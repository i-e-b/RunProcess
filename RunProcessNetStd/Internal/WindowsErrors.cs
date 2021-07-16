namespace RunProcessNetStd.Internal
{
	/// <summary>
	/// Windows API error codes.
	/// Descriptions are generally unhelpful, if you get one of these, dig through MSDN.
	/// </summary>
	public static class WindowsErrors
	{
		/// <summary>
		/// The parameter is incorrect.
		/// </summary>
		public const int InvalidArgument = 87;

		/// <summary>
		/// The system could not find the environment option that was entered.
		/// </summary>
		public const int BadEnvironmentOption = 203;

		/// <summary>
		/// There are no more files.
		/// </summary>
		public const int NoMoreFiles = 18;

		/// <summary>
		/// The handle is invalid (usually referring to a closed resource)
		/// </summary>
		public const int HandleIsInvalid = 6;
	}
}
