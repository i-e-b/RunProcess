using System;
using System.Runtime.InteropServices;

namespace RunProcess
{
	public static class Kernel32
	{
		public const int HANDLE_FLAG_INHERIT = 1;
		public const UInt32 STARTF_USESTDHANDLES = 0x00000100;
		public const UInt32 STARTF_USESHOWWINDOW = 0x00000001;

		public struct SECURITY_ATTRIBUTES
		{
			public int length;
			public IntPtr lpSecurityDescriptor;
			[MarshalAs(UnmanagedType.Bool)]
			public bool bInheritHandle;
		}

		public struct STARTUPINFO
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

		public struct PROCESS_INFORMATION
		{
			public IntPtr hProcess;
			public IntPtr hThread;
			public uint dwProcessId;
			public uint dwThreadId;
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
		                                        ref STARTUPINFO lpStartupInfo,
		                                        out PROCESS_INFORMATION lpProcessInformation);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool CreatePipe(out IntPtr hReadPipe,
		                                     out IntPtr hWritePipe,
		                                     ref SECURITY_ATTRIBUTES lpPipeAttributes,
		                                     uint nSize);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern unsafe bool PeekNamedPipe(IntPtr hNamedPipe,
		                                               IntPtr pBuffer,
		                                               int nBufferSize,
		                                               IntPtr lpBytesRead,
		                                               int* lpTotalBytesAvail,
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