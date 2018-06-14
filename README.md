RunProcess
==========

A process host for Windows (Vista and later) that is more reliable and flexible than System.Diagnostics.Process

Allows run-time reading from std error and output, and writing to std input.


Usage
-----
Like this
```csharp
using (var proc = new ProcessHost("my.exe", @"C:\temp\")) {
    proc.Start();
}
```

Or,
```csharp
using (var proc = new ProcessHost(msBuildExe, projectDir)) {
    proc.Start(projectFile + " /t:Publish");
        
    int resultCode;
    if (!proc.WaitForExit(TimeSpan.FromMinutes(1), out resultCode))
    {
        proc.Kill();
        throw new Exception("Publish killed -- took too long");
    }

    File.AppendAllText(logFile, proc.StdOut.ReadAllText(Encoding.UTF8));
    File.AppendAllText(logFile, proc.StdErr.ReadAllText(Encoding.UTF8));

    if (resultCode != 0)
    {
        throw new Exception("Publish failure: see \"" + logFile + "\" for details");
    }
}
```