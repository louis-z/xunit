﻿using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Internal;

namespace Xunit.v3
{
	/// <summary>
	/// This message indicates that a test case is about to start executing.
	/// </summary>
	public class _TestCaseStarting : _TestCaseMessage, _ITestCaseMetadata
	{
		string? testCaseDisplayName;
		Dictionary<string, List<string>> traits = new Dictionary<string, List<string>>();

		/// <inheritdoc/>
		public string? SkipReason { get; set; }

		/// <inheritdoc/>
		public string? SourceFilePath { get; set; }

		/// <inheritdoc/>
		public int? SourceLineNumber { get; set; }

		/// <inheritdoc/>
		public string TestCaseDisplayName
		{
			get => testCaseDisplayName ?? throw new InvalidOperationException($"Attempted to get {nameof(TestCaseDisplayName)} on an uninitialized '{GetType().FullName}' object");
			set => testCaseDisplayName = Guard.ArgumentNotNullOrEmpty(nameof(TestCaseDisplayName), value);
		}

		/// <inheritdoc/>
		public Dictionary<string, List<string>> Traits
		{
			get => traits;
			set => traits = value ?? new Dictionary<string, List<string>>();
		}

		IReadOnlyDictionary<string, IReadOnlyList<string>> _ITestCaseMetadata.Traits => traits.ToReadOnly();
	}
}
