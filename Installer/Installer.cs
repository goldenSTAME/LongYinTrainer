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
            throw new IOException("请把安装器放到游戏根目录，与 LongYinLiZhiZhuan.exe 放在一起，再双击。也可在安装窗口选择游戏目录。");
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
        bool present = Directory.Exists(Path.Combine(root, "BepInEx/core")) || File.Exists(Path.Combine(root,"winhttp.dll")) || File.Exists(doorstop);
        if (!present) return false;
        if (!File.Exists(core) || !File.Exists(SafePath(root,"BepInEx/core/BepInEx.Unity.IL2CPP.dll")) || !File.Exists(SafePath(root,"winhttp.dll")) || !File.Exists(SafePath(root,"dotnet/coreclr.dll")) || !File.Exists(doorstop))
            throw new IOException("发现其他加载器或不完整的 BepInEx，已停止以保留原文件。请按安装教程检查现有加载器。");
        if (AssemblyName.GetAssemblyName(core).Version.Major != 6) throw new IOException("现有 BepInEx 不是版本 6，请先检查加载器版本。");
        string ini = File.ReadAllText(doorstop).Replace("/", "\\");
        if (!System.Text.RegularExpressions.Regex.IsMatch(ini, @"(?im)^\s*enabled\s*=\s*true\s*$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(ini, @"(?im)^\s*target_assembly\s*=\s*BepInEx\\core\\BepInEx.Unity.IL2CPP.dll\s*$"))
            throw new IOException("现有 Doorstop 配置未启用 BepInEx IL2CPP，请先检查配置；安装器不会覆盖其他加载器配置。");
        return true;
    }
    // Keep overwritten originals outside plugins. Each write is journaled before mutation.
    internal static string Apply(string source, string target, Action<int> afterWrite)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        foreach (var file in files) SafePath(target, file.Substring(source.TrimEnd('\\').Length + 1));
        string backup = SafePath(target, "ModBackups/Installer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var touched = new List<string>(); var originals = new Dictionary<string,string>();
        try
        {
            foreach (var file in files)
            {
                string relative = file.Substring(source.TrimEnd('\\').Length + 1);
                string dest = SafePath(target, relative);
                if (File.Exists(dest))
                {
                    string copy = SafePath(backup, "original/" + relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(dest, copy, false); originals.Add(dest, copy);
                }
                File.AppendAllText(Path.Combine(backup,"manifest.txt"), (originals.ContainsKey(dest) ? "REPLACE " : "NEW ") + relative + Environment.NewLine);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)); touched.Add(dest);
                File.Copy(file, dest, true);
                if (Hash(file) != Hash(dest)) throw new IOException("写入校验失败：" + relative);
                if (afterWrite != null) afterWrite(touched.Count);
            }
            File.WriteAllText(Path.Combine(backup,"SUCCESS.txt"), "安装成功。original 保存被覆盖文件；manifest 记录新增和覆盖路径。卸载不撤销存档修改。");
            return backup;
        }
        catch (Exception error)
        {
            var failures = new List<string>();
            for (int i=touched.Count-1;i>=0;i--)
            {
                string path = touched[i];
                try { if (originals.ContainsKey(path)) File.Copy(originals[path],path,true); else if (File.Exists(path)) File.Delete(path); }
                catch (Exception rollback) { failures.Add(path + ": " + rollback.Message); }
            }
            string details = failures.Count == 0 ? "已回滚本次文件修改。" : "部分文件未能回滚：\r\n" + String.Join("\r\n", failures);
            throw new IOException(error.Message + "\r\n" + details + "\r\n备份：" + backup, error);
        }
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
    readonly Label info = new Label(); readonly ProgressBar progress = new ProgressBar(); readonly Button retry = new Button();
    string root = AppDomain.CurrentDomain.BaseDirectory; bool busy;
    public InstallerWindow()
    {
        Text = "龙胤立志传 修改器 · 一键安装"; Width=630; Height=355; StartPosition=FormStartPosition.CenterScreen;
        Font = new System.Drawing.Font("Microsoft YaHei UI",10); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        info.SetBounds(22,20,570,175); info.Text="准备安装 BepInEx + 修改器 0.4.26…";
        progress.SetBounds(22,205,570,22); progress.Style=ProgressBarStyle.Marquee;
        retry.SetBounds(22,246,190,36); retry.Text="选择游戏目录并安装"; retry.Enabled=false;
        retry.Click += async delegate { using(var picker=new FolderBrowserDialog()) { picker.Description="选择包含 LongYinLiZhiZhuan.exe 的游戏目录"; if(picker.ShowDialog()==DialogResult.OK) { root=picker.SelectedPath; await RunInstall(); } } };
        Controls.Add(info); Controls.Add(progress); Controls.Add(retry);
        Shown += async delegate { await RunInstall(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if(busy) e.Cancel=true; };
    }
    void Report(string value) { BeginInvoke((Action)(() => info.Text=value)); }
    async Task RunInstall()
    {
        busy=true; retry.Enabled=false; progress.Style=ProgressBarStyle.Marquee;
        try
        {
            string target=root;
            string backup=await Task.Run(() => Install(target));
            info.Text="安装完成！\r\n启动游戏并读取存档，按 H 打开 / 收起修改器。\r\n首次启动 BepInEx 需要生成接口，可能还需联网，请耐心等待。\r\n\r\n这是较为暴力的修改，会影响游戏平衡与体验；修改前备份存档。\r\n备份位置："+backup;
            progress.Style=ProgressBarStyle.Blocks; progress.Value=100;
        }
        catch (UnauthorizedAccessException ex) { info.Text="权限不足，安装已停止。请检查目录写入权限。\r\n"+ex.Message; progress.Style=ProgressBarStyle.Blocks; }
        catch (Exception ex) { info.Text="安装未完成：\r\n"+ex.Message; progress.Style=ProgressBarStyle.Blocks; }
        finally { busy=false; retry.Enabled=true; }
    }
    string Install(string target)
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
        return InstallCore.Apply(stage,target,null);
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
