using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;

namespace ManagedFusion.Build
{
	/// <summary>
	/// 
	/// </summary>
	public class PrepareForContentDeliveryNetwork : Task
	{
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
		/// Gets or sets the host.
		/// </summary>
		/// <value>The host.</value>
		[Required]
		public string Host
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
		/// Gets or sets the processed files.
		/// </summary>
		/// <value>The processed files.</value>
		[Output]
		public ITaskItem[] ProcessedFiles
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
		/// Compresses the specified file.
		/// </summary>
		/// <param name="file">The file.</param>
		private string Process(ITaskItem file, string type)
		{
			Log.LogMessage(MessageImportance.High, "Adding " + Host + " to " + file.ItemSpec);

			StringBuilder builder = new StringBuilder(File.ReadAllText(file.ItemSpec));
			//builder.Replace("url(", "url(" + Host);
			builder.Replace("AlphaImageLoader(src=", "AlphaImageLoader(src=" + Host);

			// replace the contents 
			File.WriteAllText(file.ItemSpec, builder.ToString());

			return file.ItemSpec;
		}

		/// <summary>
		/// Executes this instance.
		/// </summary>
		/// <returns></returns>
		public override bool Execute()
		{
			List<ITaskItem> processedFiles = new List<ITaskItem>(Files.Length);
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
							// compress the file
							string processedFile = Process(file, type);

							// add the file to the list of successfully compressed files
							processedFiles.Add(new TaskItem(processedFile));
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

			// return all the new processed files
			ProcessedFiles = processedFiles.ToArray();

			// return if there were any errors while running this task
			return !Log.HasLoggedErrors;
		}
	}
}
