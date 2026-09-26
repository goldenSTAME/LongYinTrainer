using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class InstallCore
{
    internal const string LoaderUrl = "https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip";
    internal const string LoaderHash = "2A7CBF74D26ABE4765C3E662DB1721B923BAC39849EBFEF2CA5DC7DE7E2D9B7F";
    internal static string Hash(string file)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    internal static string SafePath(string root, string relative)
    {
        if (String.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new IOException("安装包路径无效：" + relative);
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("安装包路径超出目标目录。");
        for (string check = path; check != null; check = Path.GetDirectoryName(check))
            if ((File.Exists(check) || Directory.Exists(check)) && (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("检测到链接目录或文件，请使用普通游戏目录：" + check);
        return path;
    }
    internal static void Extract(string zip, string directory)
    {
        using (var archive = ZipFile.OpenRead(zip))
        {
            foreach (var entry in archive.Entries)
            {
                string path = SafePath(directory, entry.FullName);
                if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                entry.ExtractToFile(path, false);
            }
        }
    }
    internal static void ValidateGame(string root, bool checkProcess)
    {
        string exe = SafePath(root, "LongYinLiZhiZhuan.exe");
        if (!File.Exists(exe) || !File.Exists(SafePath(root, "GameAssembly.dll")) || !Directory.Exists(SafePath(root, "LongYinLiZhiZhuan_Data")))
            throw new IOException("请把安装器放到游戏根目录，与 LongYinLiZhiZhuan.exe 放在一起，再双击。");
        using (var reader = new BinaryReader(File.OpenRead(exe)))
        {
            if (reader.ReadUInt16() != 0x5a4d) throw new IOException("游戏 EXE 格式无效。");
            reader.BaseStream.Position = 0x3c;
            int offset = reader.ReadInt32(); reader.BaseStream.Position = offset;
            if (reader.ReadUInt32() != 0x4550 || reader.ReadUInt16() != 0x8664) throw new IOException("只支持 Windows x64 游戏。");
        }
        if (checkProcess)
            foreach (var p in Process.GetProcessesByName("LongYinLiZhiZhuan"))
            { p.Dispose(); throw new IOException("游戏正在运行，请正常退出游戏后重新安装。"); }
    }
    internal static bool HasCompatibleLoader(string root)
    {
        string core = SafePath(root, "BepInEx/core/BepInEx.Core.dll");
        string doorstop = SafePath(root, "doorstop_config.ini");
        if (!File.Exists(core) || !File.Exists(SafePath(root,"BepInEx/core/BepInEx.Unity.IL2CPP.dll")) || !File.Exists(SafePath(root,"winhttp.dll")) || !File.Exists(SafePath(root,"dotnet/coreclr.dll")) || !File.Exists(doorstop)) return false;
        try { if (AssemblyName.GetAssemblyName(core).Version.Major != 6) return false; }
        catch (BadImageFormatException) { return false; }
        string ini = File.ReadAllText(doorstop).Replace("/", "\\");
        if (!System.Text.RegularExpressions.Regex.IsMatch(ini, @"(?im)^\s*enabled\s*=\s*true\s*$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(ini, @"(?im)^\s*target_assembly\s*=\s*BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\s*$")) return false;
        return true;
    }
    internal static void Apply(string source, string target)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        foreach (var file in files) SafePath(target, file.Substring(source.TrimEnd('\\').Length + 1));
        foreach (var file in files)
        {
            string relative = file.Substring(source.TrimEnd('\\').Length + 1);
            string dest = SafePath(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
            if (Hash(file) != Hash(dest)) throw new IOException("写入校验失败，请重新安装：" + relative);
        }
    }
    internal static void ConfigureGameLoader(string target)
    {
        string path=SafePath(target,"BepInEx/config/BepInEx.cfg");
        var lines=new List<string>(File.Exists(path) ? File.ReadAllLines(path) : new string[0]);
        SetConfig(lines,"Logging","UnityLogListening","false");
        SetConfig(lines,"Logging.Console","Enabled","false");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllLines(path,lines);
    }
    static void SetConfig(List<string> lines,string section,string key,string value)
    {
        int start=-1,end=lines.Count;
        for(int i=0;i<lines.Count;i++)
        {
            string line=lines[i].Trim();
            if(line=="["+section+"]") { start=i; continue; }
            if(start>=0 && line.StartsWith("[")) { end=i; break; }
        }
        if(start<0) { lines.Add(""); lines.Add("["+section+"]"); lines.Add(key+" = "+value); return; }
        bool found=false;
        for(int i=start+1;i<end;i++)
        {
            string line=lines[i].Trim(); int eq=line.IndexOf('=');
            if(eq>0 && line.Substring(0,eq).Trim()==key) { lines[i]=key+" = "+value; found=true; }
        }
        if(!found) lines.Insert(end,key+" = "+value);
    }
}

internal sealed class InstallerWindow : Form
{
    sealed class DownloadClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address); request.Timeout = 120000;
            var http = request as HttpWebRequest; if (http != null) http.ReadWriteTimeout = 120000;
            return request;
        }
    }
    readonly Label info = new Label(); readonly ProgressBar progress = new ProgressBar(); readonly Button closeButton = new Button();
    string root = AppDomain.CurrentDomain.BaseDirectory; bool busy;
    public InstallerWindow()
    {
        Text = "龙胤立志传 修改器 · 一键安装 0.4.26.2"; Width=630; Height=355; StartPosition=FormStartPosition.CenterScreen;
        Font = new System.Drawing.Font("Microsoft YaHei UI",10); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        info.SetBounds(22,20,570,175); info.Text="准备安装 BepInEx + 修改器 0.4.26…";
        progress.SetBounds(22,205,570,22); progress.Style=ProgressBarStyle.Marquee;
        closeButton.SetBounds(22,246,190,36); closeButton.Text="关闭"; closeButton.Enabled=false;
        closeButton.Click += delegate { Close(); };
        Controls.Add(info); Controls.Add(progress); Controls.Add(closeButton);
        Shown += async delegate { await RunInstall(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if(busy) e.Cancel=true; };
    }
    void Report(string value) { BeginInvoke((Action)(() => info.Text=value)); }
    async Task RunInstall()
    {
        busy=true; closeButton.Enabled=false; progress.Style=ProgressBarStyle.Marquee;
        try
        {
            string target=root;
            await Task.Run(() => Install(target));
            info.Text="安装完成！\r\n启动游戏并读取存档，按 H 打开 / 收起修改器。\r\n首次启动 BepInEx 需要生成接口，可能还需联网，请耐心等待。\r\n\r\n这是较为暴力的修改，会影响游戏平衡与体验；修改前备份存档。";
            progress.Style=ProgressBarStyle.Blocks; progress.Value=100;
            closeButton.Text="完成并关闭";
        }
        catch (UnauthorizedAccessException ex) { info.Text="权限不足，安装已停止。请检查目录写入权限。\r\n"+ex.Message; progress.Style=ProgressBarStyle.Blocks; }
        catch (Exception ex) { info.Text="安装未完成：\r\n"+ex.Message; progress.Style=ProgressBarStyle.Blocks; }
        finally { busy=false; closeButton.Enabled=true; }
    }
    void Install(string target)
    {
        InstallCore.ValidateGame(target,true);
        bool existing=InstallCore.HasCompatibleLoader(target);
        string pluginDir=InstallCore.SafePath(target,"BepInEx/plugins");
        if(Directory.Exists(pluginDir))
            foreach(var duplicate in Directory.GetFiles(pluginDir,"LongYinTrainer.dll",SearchOption.AllDirectories))
                if(!String.Equals(Path.GetFullPath(duplicate),Path.Combine(pluginDir,"LongYinTrainer.dll"),StringComparison.OrdinalIgnoreCase))
                    throw new IOException("发现子目录中的旧 Mod，请先将它移出 plugins，避免重复加载："+duplicate);
        string probe=InstallCore.SafePath(target,".longyin-write-test-"+Guid.NewGuid().ToString("N"));
        using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)) file.WriteByte(0);
        string temp=Path.Combine(Path.GetTempPath(),"LongYinInstaller-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        string stage=Path.Combine(temp,"stage"); Directory.CreateDirectory(stage);
        if (!existing)
        {
            Report("正在从 BepInEx 官方下载已验证的 IL2CPP x64 加载器（约 33 MB）…\r\n下载需要联网，关闭窗口将在安装结束后恢复。\r\n目标："+target);
            string loader=Path.Combine(temp,"BepInEx.zip");
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            using(var client=new DownloadClient()) client.DownloadFile(InstallCore.LoaderUrl,loader);
            if(InstallCore.Hash(loader)!=InstallCore.LoaderHash) throw new IOException("BepInEx 下载校验失败，尚未修改游戏文件。请重新运行安装器。");
            InstallCore.Extract(loader,stage);
        }
        Report(existing ? "检测到 BepInEx 6 IL2CPP，保留加载器、配置与其他插件，正在安装修改器…" : "BepInEx 校验成功，正在安装加载器与修改器…");
        string mod=Path.Combine(temp,"Mod.zip");
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ModPayload")) using(var file=File.Create(mod)) resource.CopyTo(file);
        // Documentation lives in its own directory instead of overwriting a game's INSTALL.md.
        string modStage=Path.Combine(temp,"mod"); Directory.CreateDirectory(modStage); InstallCore.Extract(mod,modStage);
        string plugins=Path.Combine(stage,"BepInEx/plugins"); Directory.CreateDirectory(plugins);
        File.Copy(Path.Combine(modStage,"BepInEx/plugins/LongYinTrainer.dll"),Path.Combine(plugins,"LongYinTrainer.dll"),true);
        string docs=Path.Combine(stage,"LongYinTrainer-说明"); Directory.CreateDirectory(docs);
        foreach(var name in new[]{"INSTALL.md","USAGE.md"}) File.Copy(Path.Combine(modStage,name),Path.Combine(docs,name));
        InstallCore.ValidateGame(target,true);
        InstallCore.Apply(stage,target);
        // This game's Unity build crashes in IL2CPPUnityLogSource before plugins load.
        // Keep disk logging; disable only Unity log interception and the extra console.
        InstallCore.ConfigureGameLoader(target);
    }
    [STAThread] static void Main()
    {
        bool created; using(var mutex=new System.Threading.Mutex(true,"Local\\LongYinTrainerInstaller",out created))
        {
            if(!created) { MessageBox.Show("已有安装器在运行。","龙胤立志传 修改器"); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new InstallerWindow());
        }
    }
}



