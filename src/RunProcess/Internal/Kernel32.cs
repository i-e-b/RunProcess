using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RunProcess.Internal
{
    internal static class Kernel32
    {
        public const int HandleFlagInherit = 1;
        public const UInt32 StartfUsestdhandles = 0x00000100;
        public const UInt32 StartfUseshowwindow = 0x00000001;

        public const UInt32 UseShowWindow = 0x00000001;
        public const short SW_HIDE = 0;

        public struct SecurityAttributes
        {
            public int length;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct Startupinfo
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        public enum WaitResult : uint
        {
            WaitAbandoned = 0x00000080U,
            WaitComplete = 0,
            WaitTimeout = 0x00000102U,
            WaitFailed = 0xFFFFFFFFU
        }

        public enum LogonFlags : uint
        {
            LogonWithProfile = 0x00000001U,
            LogonNetCredentialsOnly = 0x00000002U,
            NoFlags = 0
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern int CreateProcessA(string lpApplicationName,
            string lpCommandLine, IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes, int bInheritHandles,
            ProcessCreationFlags dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
            [In] ref Startupinfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [Flags]
        public enum ProcessCreationFlags : uint
        {
            DebugProcess = 0x00000001,
            DebugOnlyThisProcess = 0x00000002,
            CreateSuspended = 0x00000004,
            CreateBreakawayFromJob = 0x01000000,
            CreateNoWindow = 0x08000000,
        }

        public enum DebugEventType
        {
            UNKNOWN = 0,
            CREATE_PROCESS_DEBUG_EVENT = 3,
            CREATE_THREAD_DEBUG_EVENT = 2,
            EXCEPTION_DEBUG_EVENT = 1,
            EXIT_PROCESS_DEBUG_EVENT = 5,
            EXIT_THREAD_DEBUG_EVENT = 4,
            LOAD_DLL_DEBUG_EVENT = 6,
            OUTPUT_DEBUG_STRING_EVENT = 8,
            RIP_EVENT = 9,
            UNLOAD_DLL_DEBUG_EVENT = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEBUG_EVENT
        {
            public DebugEventType dwDebugEventCode;
            public int dwProcessId;
            public int dwThreadId;

            // ReSharper disable once FieldCanBeMadeReadOnly.Local
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 86, ArraySubType = UnmanagedType.U1)]
            public byte[] debugInfo;

            public EXCEPTION_DEBUG_INFO Exception{
                get { return GetDebugInfo<EXCEPTION_DEBUG_INFO>(); }
            }

            public CREATE_THREAD_DEBUG_INFO CreateThread
            {
                get { return GetDebugInfo<CREATE_THREAD_DEBUG_INFO>(); }
            }

            public CREATE_PROCESS_DEBUG_INFO CreateProcessInfo
            {
                get { return GetDebugInfo<CREATE_PROCESS_DEBUG_INFO>(); }
            }

            public EXIT_THREAD_DEBUG_INFO ExitThread
            {
                get { return GetDebugInfo<EXIT_THREAD_DEBUG_INFO>(); }
            }

            public EXIT_PROCESS_DEBUG_INFO ExitProcess
            {
                get { return GetDebugInfo<EXIT_PROCESS_DEBUG_INFO>(); }
            }

            public LOAD_DLL_DEBUG_INFO LoadDll
            {
                get { return GetDebugInfo<LOAD_DLL_DEBUG_INFO>(); }
            }

            public UNLOAD_DLL_DEBUG_INFO UnloadDll
            {
                get { return GetDebugInfo<UNLOAD_DLL_DEBUG_INFO>(); }
            }

            public OUTPUT_DEBUG_STRING_INFO DebugString
            {
                get { return GetDebugInfo<OUTPUT_DEBUG_STRING_INFO>(); }
            }

            public RIP_INFO RipInfo
            {
                get { return GetDebugInfo<RIP_INFO>(); }
            }

            private T GetDebugInfo<T>() where T : struct
            {
                var structSize = Marshal.SizeOf(typeof(T));
                var pointer = Marshal.AllocHGlobal(structSize);
                Marshal.Copy(debugInfo, 0, pointer, structSize);

                var result = Marshal.PtrToStructure(pointer, typeof(T));
                Marshal.FreeHGlobal(pointer);
                return (T)result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXCEPTION_DEBUG_INFO
        {
            public EXCEPTION_RECORD ExceptionRecord;
            public uint dwFirstChance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXCEPTION_RECORD
        {
            public uint ExceptionCode;
            public uint ExceptionFlags;
            public IntPtr ExceptionRecord;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15, ArraySubType = UnmanagedType.U4)]
            public uint[] ExceptionInformation;
        }

        public delegate uint PTHREAD_START_ROUTINE(IntPtr lpThreadParameter);

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_THREAD_DEBUG_INFO
        {
            public IntPtr hThread;
            public IntPtr lpThreadLocalBase;
            public PTHREAD_START_ROUTINE lpStartAddress;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_PROCESS_DEBUG_INFO
        {
            public IntPtr hFile;
            public IntPtr hProcess;
            public IntPtr hThread;
            public IntPtr lpBaseOfImage;
            public uint dwDebugInfoFileOffset;
            public uint nDebugInfoSize;
            public IntPtr lpThreadLocalBase;
            public PTHREAD_START_ROUTINE lpStartAddress;
            public IntPtr lpImageName;
            public ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXIT_THREAD_DEBUG_INFO
        {
            public uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct EXIT_PROCESS_DEBUG_INFO
        {
            public uint dwExitCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LOAD_DLL_DEBUG_INFO
        {
            public IntPtr hFile;
            public IntPtr lpBaseOfDll;
            public uint dwDebugInfoFileOffset;
            public uint nDebugInfoSize;
            public IntPtr lpImageName;
            public ushort fUnicode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNLOAD_DLL_DEBUG_INFO
        {
            public IntPtr lpBaseOfDll;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OUTPUT_DEBUG_STRING_INFO
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpDebugStringData;
            public ushort fUnicode;
            public ushort nDebugStringLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RIP_INFO
        {
            public uint dwError;
            public uint dwType;
        }

        public enum JOBOBJECTINFOCLASS { ExtendedLimitInformation = 9 }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = false)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, IntPtr lpName);

        [DllImport("kernel32.dll", EntryPoint = "WaitForDebugEvent")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WaitForDebugEvent([In, Out] ref DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, uint dwContinueStatus);



        public static SafeWaitHandle CreateJob()
        {
            var jobObjectHandle = CreateJobObject(IntPtr.Zero, IntPtr.Zero);

            if (jobObjectHandle == IntPtr.Zero) throw new ApplicationException("CreateJobObject failed: " + Marshal.GetLastWin32Error());

            try
            {
                return new SafeWaitHandle(jobObjectHandle, true);
            }
            catch
            {
                CloseHandle(jobObjectHandle);
                throw;
            }
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern int AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern int SetInformationJobObject(IntPtr hJob,
            JOBOBJECTINFOCLASS JobObjectInfoClass, IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength);


        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(string lpApplicationName,
                                                string lpCommandLine,
                                                IntPtr lpProcessAttributes,
                                                IntPtr lpThreadAttributes,
                                                [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
                                                uint dwCreationFlags,
                                                IntPtr lpEnvironment,
                                                string lpCurrentDirectory,
                                                ref Startupinfo lpStartupInfo,
                                                out ProcessInformation lpProcessInformation);

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcessWithLogonW(string lpUsername,
                                                            string lpDomain,
                                                            string lpPassword,
                                                            LogonFlags dwLogonFlags,
                                                            string lpApplicationName,
                                                            string lpCommandLine,
                                                            uint dwCreationFlags,
                                                            IntPtr lpEnvironment,
                                                            string lpCurrentDirectory,
                                                            ref Startupinfo lpStartupInfo,
                                                            out ProcessInformation lpProcessInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess,
                                                [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                                                uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern WaitResult WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out int exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreatePipe(out IntPtr hReadPipe,
                                             out IntPtr hWritePipe,
                                             ref SecurityAttributes lpPipeAttributes,
                                             uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekNamedPipe(IntPtr hNamedPipe,
                                                IntPtr pBuffer,
                                                int nBufferSize,
                                                IntPtr lpBytesRead,
                                                IntPtr lpTotalBytesAvail,
                                                IntPtr lpBytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool ReadFile(IntPtr hFile,
                                                  void* pBuffer,
                                                  int nNumberOfBytesToRead,
                                                  int* lpNumberOfBytesRead,
                                                  IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern unsafe bool WriteFile(IntPtr hFile,
                                                   void* pBuffer,
                                                   int nNumberOfBytesToWrite,
                                                   int* lpNumberOfBytesWritten,
                                                   IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetHandleInformation(IntPtr hObject, int dwMask, uint dwFlags);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DebugActiveProcess(uint piDwProcessId);
    }
}