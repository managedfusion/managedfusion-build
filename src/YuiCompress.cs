using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Security;

namespace ManagedFusion.Build
{
	/// <summary>
	/// 
	/// </summary>
	public class YuiCompress : Task
	{
		public YuiCompress()
		{
			MinifyOnly = false;
			PreserveSemiColons = false;
			DisableOptimizations = false;
			CharacterSet = String.Empty;
			LineBreak = 0;
		}

		/// <summary>
		/// Gets or sets the type.
		/// </summary>
		/// <value>The type.</value>
		[Required]
		public string Type
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the option to minify only, and do not obfuscate, also known as no munge.
		/// </summary>
		/// <remarks>Minify only. Do not obfuscate local symbols.</remarks>
		public bool MinifyOnly
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the option to disable all micro optimizations.
		/// </summary>
		/// <remarks>Disable all the built-in micro optimizations.</remarks>
		public bool DisableOptimizations
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the option to preserve all semicolons.
		/// </summary>
		/// <remarks>Preserve unnecessary semicolons (such as right before a '}') This option
		/// is useful when compressed code has to be run through JSLint (which is the
		/// case of YUI for example)</remarks>
		public bool PreserveSemiColons
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the character set for the input file.
		/// </summary>
		/// <remarks>If a supported character set is specified, the YUI Compressor will use it
		/// to read the input file. Otherwise, it will assume that the platform's
		/// default character set is being used. The output file is encoded using
		/// the same character set.</remarks>
		public string CharacterSet
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the the column to insert a line break after.
		/// </summary>
		/// <remarks>Some source control tools don't like files containing lines longer than,
		/// say 8000 characters. The linebreak option is used in that case to split
		/// long lines after a specific column. It can also be used to make the code
		/// more readable, easier to debug (especially with the MS Script Debugger)
		/// Specify 0 to get a line break after each semi-colon in JavaScript, and
		/// after each rule in CSS.</remarks>
		public int LineBreak
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the files.
		/// </summary>
		/// <value>The files.</value>
		[Required]
		public ITaskItem[] Files
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets a value indicating whether [show warnings].
		/// </summary>
		/// <value><c>true</c> if [show warnings]; otherwise, <c>false</c>.</value>
		public bool ShowWarnings
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the compressed files.
		/// </summary>
		/// <value>The compressed files.</value>
		[Output]
		public ITaskItem[] CompressedFiles
		{
			get;
			private set;
		}

		/// <summary>
		/// Formats the warning.
		/// </summary>
		/// <param name="warning">The warning.</param>
		/// <returns></returns>
		private string FormatWarning(string warning)
		{
			return warning
				.Trim()
				.Replace("[WARNING] ", String.Empty);
		}

		/// <summary>
		/// Gets the java.exe location.
		/// </summary>
		private string JavaExeLocation
		{
			get
			{
				if (!String.IsNullOrEmpty(Properties.Settings.Default.JavaExeLocation))
					return Properties.Settings.Default.JavaExeLocation;

				// it has not been automatically set so lets use the default install path for common Java installs
				switch (ProcessorArchitecture.CurrentProcessArchitecture)
				{
					case ProcessorArchitecture.AMD64:
					case ProcessorArchitecture.IA64:
						return @"%PROGRAMFILES(X86)%\Java\jre6\bin\java.exe";

					case ProcessorArchitecture.X86:
					case ProcessorArchitecture.MSIL:
					default:
						return @"%PROGRAMFILES%\Java\jre6\bin\java.exe";
				}
			}
		}

		/// <summary>
		/// Compresses the specified file.
		/// </summary>
		/// <param name="file">The file.</param>
		private string Compress(ITaskItem file, string type)
		{
			string oldFile = file.ItemSpec;
			string newFile = oldFile.Replace("." + type, "-min." + type);
			string args = String.Empty;

			Log.LogMessage(MessageImportance.High, "Compressing " + oldFile + " to " + newFile.Substring(newFile.LastIndexOf('\\') +1));

			if (String.Equals(type, "css", StringComparison.InvariantCultureIgnoreCase))
			{
				args = "--type css";
			}
			else
			{
				args = "--type js";

				if (MinifyOnly)
					args += " --nomunge";

				if (PreserveSemiColons)
					args += " --preserve-semi";

				if (DisableOptimizations)
					args += " --disable-optimizations";
			}

			if (LineBreak > 0)
				args += " --line-break " + LineBreak;

			if (!String.IsNullOrEmpty(CharacterSet))
				args += " --charset " + CharacterSet;

			if (ShowWarnings)
				args += " --verbose";

			Process process = new Process();
			process.StartInfo = new ProcessStartInfo {
				FileName = JavaExeLocation,
				Arguments = String.Format(
					@"-jar ""{0}"" {1} -o ""{2}"" ""{3}""",
					Properties.Settings.Default.YuiCompressorJarLocation,
					args,
					newFile,
					oldFile
				),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			process.Start();
			process.WaitForExit(5000);

			// log any messages or warnings
			string[] warnings = process.StandardError.ReadToEnd()
				.Replace("\r", String.Empty)
				.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

			foreach(string warning in warnings)
				Log.LogWarning(null, null, null, oldFile, 1, 1, 1, 1, FormatWarning(warning), null);

			return newFile;
		}

		/// <summary>
		/// Executes this instance.
		/// </summary>
		/// <returns></returns>
		public override bool Execute()
		{
			List<ITaskItem> compressedFiles = new List<ITaskItem>(Files.Length);
			string type = Type.ToLower();

			foreach (ITaskItem file in Files)
			{
				// make sure the file at least has a value before compressing
				if (file.ItemSpec.Length > 0)
				{
					try
					{
						if (File.Exists(file.ItemSpec))
						{
							// delete any old files already compressed
							if (file.ItemSpec.EndsWith("-min." + type))
								File.Delete(file.ItemSpec);
							else
							{
								// compress the file
								string compressedFile = Compress(file, type);

								// add the file to the list of successfully compressed files
								compressedFiles.Add(new TaskItem(compressedFile));
							}
						}
						else
						{
							Log.LogError("Error in trying to find " + file.ItemSpec + ", it doesn't exist.");
						}
					}
					catch (Exception ex)
					{
						if (ex is IOException
							|| ex is UnauthorizedAccessException
							|| ex is PathTooLongException
							|| ex is DirectoryNotFoundException
							|| ex is SecurityException)
						{
							Log.LogErrorFromException(ex, false, true, file.ItemSpec);
						}
						else
						{
							throw;
						}
					}
				}
			}

			// return all the new compressed files
			CompressedFiles = compressedFiles.ToArray();

			// return if there were any errors while running this task
			return !Log.HasLoggedErrors;
		}
	}
}
