using Avalonia86.API;
using System.Diagnostics;
using System.IO;

namespace Avalonia86.Common;

public abstract class CommonExecutor : IExecutor
{
    //Issues with this code
    // 1. It is realtive to the working directory of the selected executable, not the working directory of the default executable.
    // 2. The UI is not tested or set up for relative paths.
    private string ResolvePath(string path, string workingDir)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path.StartsWith(".."))
            return Path.GetFullPath(Path.Combine(workingDir, path));

        return path;
    }

    public virtual ProcessStartInfo BuildStartInfo(IExecVars args)
    {
        var workingDirectory = Path.GetDirectoryName(args.FileName);

        var info = new ProcessStartInfo(args.FileName);
        var ops = info.ArgumentList;

        var romPath = ResolvePath(args.RomPath, workingDirectory);
        var assetPath = ResolvePath(args.AssetPath, workingDirectory);
        var logFile = ResolvePath(args.LogFile, workingDirectory);
        var vmPath = ResolvePath(args.VmPath, workingDirectory);

        if (!string.IsNullOrWhiteSpace(romPath) && args.Build >= 3333)
        {
            ops.Add("-R");
            ops.Add(romPath);
        }

        if (!string.IsNullOrWhiteSpace(assetPath) && args.Build >= 3333)
        {
            ops.Add("-A");
            ops.Add(assetPath);
        }

        if (!string.IsNullOrWhiteSpace(logFile))
        {
            ops.Add("-L");
            ops.Add(logFile);
        }

        ops.Add("-P");
        ops.Add(vmPath);

        if (args.Build >= 3333)
        {
            ops.Add("-V");
            ops.Add(args.Vm.Title);
        }

        info.WorkingDirectory = args.VmPath;
        return info;
    }

    public virtual ProcessStartInfo BuildConfigInfo(IExecVars args)
    {
        var workingDirectory = Path.GetDirectoryName(args.FileName);

        var romPath = ResolvePath(args.RomPath, workingDirectory);
        var assetPath = ResolvePath(args.AssetPath, workingDirectory);
        var vmPath = ResolvePath(args.VmPath, workingDirectory);

        var info = new ProcessStartInfo(args.FileName);
        var ops = info.ArgumentList;
        if (!string.IsNullOrWhiteSpace(romPath) && args.Build >= 3333)
        {
            ops.Add("-R");
            ops.Add(romPath);
        }
        if (!string.IsNullOrWhiteSpace(assetPath) && args.Build >= 3333)
        {
            ops.Add("-A");
            ops.Add(assetPath);
        }
        ops.Add("--settings");
        ops.Add("-P");
        ops.Add(vmPath);
        info.WorkingDirectory = vmPath;
        return info;
    }
}