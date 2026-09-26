using System;
using System.IO;
using System.IO.Compression;
class InstallerTests
{
    static int count;
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); count++; }
    static void Reject(Action action,string name) { bool failed=false; try {action();} catch(IOException) {failed=true;} Check(failed,name); }
    static void Main(string[] args)
    {
        string root=Path.Combine(Path.GetTempPath(),"LongYinInstallerTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Reject(()=>InstallCore.SafePath(root,"../escape"),"reject traversal");
        Reject(()=>InstallCore.SafePath(root,"C:\\escape"),"reject absolute path");
        Reject(()=>InstallCore.SafePath(root,"x:stream"),"reject alternate stream");
        Reject(()=>InstallCore.ValidateGame(root,false),"reject non-game directory");
        string src=Path.Combine(root,"src"),dest=Path.Combine(root,"game"); Directory.CreateDirectory(src); Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(src,"mod.dll"),"new"); File.WriteAllText(Path.Combine(dest,"mod.dll"),"old"); File.WriteAllText(Path.Combine(dest,"config.txt"),"keep");
        string backup=InstallCore.Apply(src,dest,null);
        Check(File.ReadAllText(Path.Combine(dest,"mod.dll"))=="new" && File.ReadAllText(Path.Combine(backup,"original/mod.dll"))=="old","update backs up original");
        Check(File.ReadAllText(Path.Combine(dest,"config.txt"))=="keep","preserve unrelated configuration");
        File.WriteAllText(Path.Combine(src,"extra.dll"),"added"); File.WriteAllText(Path.Combine(dest,"mod.dll"),"before");
        Reject(()=>InstallCore.Apply(src,dest,n=>{if(n==2)throw new IOException("simulated failure");}),"injected write failure reported");
        Check(File.ReadAllText(Path.Combine(dest,"mod.dll"))=="before" && !File.Exists(Path.Combine(dest,"extra.dll")),"rollback restores originals and removes new files");
        string fresh=Path.Combine(root,"fresh"); Directory.CreateDirectory(fresh); InstallCore.Apply(src,fresh,null);
        Check(File.Exists(Path.Combine(fresh,"extra.dll")),"fresh install");
        string zip=Path.Combine(root,"bad.zip"); using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)) {using(var w=new StreamWriter(a.CreateEntry("../escape.txt").Open()))w.Write("bad");}
        Reject(()=>InstallCore.Extract(zip,Path.Combine(root,"extract")),"reject malicious archive entry");
        Check(InstallCore.Hash(args[0])==InstallCore.LoaderHash,"official loader hash matches pinned version");
        string loader=Path.Combine(root,"loader"); InstallCore.Extract(args[0],loader);
        Check(InstallCore.HasCompatibleLoader(loader),"official loader recognized as compatible");
        Check(!InstallCore.HasCompatibleLoader(fresh),"fresh game needs loader");
        string broken=Path.Combine(root,"broken"); Directory.CreateDirectory(broken); File.WriteAllText(Path.Combine(broken,"winhttp.dll"),"other");
        Reject(()=>InstallCore.HasCompatibleLoader(broken),"refuse incomplete or conflicting loader");
        Console.WriteLine(count+" tests passed. Fixtures: "+root);
    }
}
