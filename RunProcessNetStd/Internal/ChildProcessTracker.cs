using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace RunProcessNetStd.Internal
{
    /// <summary>
    /// Allows processes to be automatically killed if this parent process unexpectedly quits.
    /// This feature requires Windows 8 or greater. On Windows 7, nothing is done.</summary>
    /// <remarks>References:
    ///  https://stackoverflow.com/a/4657392/386091
    ///  https://stackoverflow.com/a/9164742/386091 </remarks>
    public static class ChildProcessTracker
    {
        /// <summary>
        /// Add the process to be tracked. If our current process is killed, the child processes
        /// that we are tracking will be automatically killed, too. If the child process terminates
        /// first, that's fine, too.</summary>
        public static void AddProcess(IntPtr processHandle)
        {
            if (s_jobHandle == IntPtr.Zero) throw new Exception("Can't start child processes");
            var success = AssignProcessToJobObject(s_jobHandle, processHandle);
            if (!success) throw new Win32Exception();
        }

        static ChildProcessTracker()
        {
            // This feature requires Windows 8 or later. To support Windows 7 requires
            //  registry settings to be added if you are using Visual Studio plus an
            //  app.manifest change.
            //  https://stackoverflow.com/a/4232259/386091
            //  https://stackoverflow.com/a/9507862/386091
            if (Environment.OSVersion.Version < new Version(6, 2))
                return;

            // The job name is optional (and can be null) but it helps with diagnostics.
            //  If it's not null, it has to be unique. Use SysInternals' Handle command-line
            //  utility: handle -a ChildProcessTracker
            string jobName = "ChildProcessTracker" + Process.GetCurrentProcess().Id;
            s_jobHandle = CreateJobObject(IntPtr.Zero, jobName);

            // This is the key flag. When our process is killed, Windows will automatically
            //  close the job handle, and when that happens, we want the child processes to
            //  be killed, too.
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION {LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE};

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION {BasicLimitInformation = info};

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                    extendedInfoPtr, (uint)length))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

        [DllImport("kernel32.dll")]
        static extern bool SetInformationJobObject(IntPtr job, JobObjectInfoType infoType,
            IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        // Windows will automatically close any open job handles when our process terminates.
        //  This can be verified by using SysInternals' Handle utility. When the job handle
        //  is closed, the child processes will be killed.
        private static readonly IntPtr s_jobHandle;
    }

    /// <summary>
    /// Win32 Job info type
    /// </summary>
    public enum JobObjectInfoType
    {
        /// <summary>AssociateCompletionPortInformation</summary>
        AssociateCompletionPortInformation = 7,
        /// <summary>BasicLimitInformation</summary>
        BasicLimitInformation = 2,
        /// <summary>BasicUIRestrictions</summary>
        BasicUIRestrictions = 4,
        /// <summary>EndOfJobTimeInformation</summary>
        EndOfJobTimeInformation = 6,
        /// <summary>ExtendedLimitInformation</summary>
        ExtendedLimitInformation = 9,
        /// <summary>SecurityLimitInformation</summary>
        SecurityLimitInformation = 5,
        /// <summary>GroupInformation</summary>
        GroupInformation = 11
    }

    /// <summary>
    /// Win32 job limit information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        /// <summary></summary>
        public Int64 PerProcessUserTimeLimit;
        /// <summary></summary>
        public Int64 PerJobUserTimeLimit;
        /// <summary></summary>
        public JOBOBJECTLIMIT LimitFlags;
        /// <summary></summary>
        public UIntPtr MinimumWorkingSetSize;
        /// <summary></summary>
        public UIntPtr MaximumWorkingSetSize;
        /// <summary></summary>
        public UInt32 ActiveProcessLimit;
        /// <summary></summary>
        public Int64 Affinity;
        /// <summary></summary>
        public UInt32 PriorityClass;
        /// <summary></summary>
        public UInt32 SchedulingClass;
    }

    /// <summary>
    /// Limit flags
    /// </summary>
    [Flags]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public enum JOBOBJECTLIMIT : uint
    {
        /// <summary></summary>
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
    }

    /// <summary>
    /// Job IO limit counters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public struct IO_COUNTERS
    {
        /// <summary></summary>
        public UInt64 ReadOperationCount;
        /// <summary></summary>
        public UInt64 WriteOperationCount;
        /// <summary></summary>
        public UInt64 OtherOperationCount;
        /// <summary></summary>
        public UInt64 ReadTransferCount;
        /// <summary></summary>
        public UInt64 WriteTransferCount;
        /// <summary></summary>
        public UInt64 OtherTransferCount;
    }
    
    /// <summary>
    /// Job limit counters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        /// <summary></summary>
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        /// <summary></summary>
        public IO_COUNTERS IoInfo;
        /// <summary></summary>
        public UIntPtr ProcessMemoryLimit;
        /// <summary></summary>
        public UIntPtr JobMemoryLimit;
        /// <summary></summary>
        public UIntPtr PeakProcessMemoryUsed;
        /// <summary></summary>
        public UIntPtr PeakJobMemoryUsed;
    }
}