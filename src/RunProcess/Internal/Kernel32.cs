using System;
using System.Runtime.InteropServices;

namespace RunProcess.Internal
{
	internal static class Kernel32
	{
		public const int HandleFlagInherit = 1;
		public const UInt32 StartfUsestdhandles = 0x00000100;
		public const UInt32 StartfUseshowwindow = 0x00000001;

		public struct SecurityAttributes
		{
			public int length;
			public IntPtr lpSecurityDescriptor;
			[MarshalAs(UnmanagedType.Bool)]
			public bool bInheritHandle;
		}

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
		public static extern bool CreateProcessWithLogonW  (string lpUsername,
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
	}
}