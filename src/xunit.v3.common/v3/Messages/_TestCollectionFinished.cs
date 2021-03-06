﻿using System;

namespace Xunit.v3
{
	/// <summary>
	/// This message indicates that a test collection has just finished executing (meaning,
	/// all the test classes in the collection has finished).
	/// </summary>
	public class _TestCollectionFinished : _TestCollectionMessage, _IExecutionSummaryMetadata
	{
		decimal? executionTime;
		int? testsFailed;
		int? testsRun;
		int? testsSkipped;

		/// <inheritdoc/>
		public decimal ExecutionTime
		{
			get => executionTime ?? throw new InvalidOperationException($"Attempted to get {nameof(ExecutionTime)} on an uninitialized '{GetType().FullName}' object");
			set => executionTime = value;
		}

		/// <inheritdoc/>
		public int TestsFailed
		{
			get => testsFailed ?? throw new InvalidOperationException($"Attempted to get {nameof(TestsFailed)} on an uninitialized '{GetType().FullName}' object");
			set => testsFailed = value;
		}

		/// <inheritdoc/>
		public int TestsRun
		{
			get => testsRun ?? throw new InvalidOperationException($"Attempted to get {nameof(TestsRun)} on an uninitialized '{GetType().FullName}' object");
			set => testsRun = value;
		}

		/// <inheritdoc/>
		public int TestsSkipped
		{
			get => testsSkipped ?? throw new InvalidOperationException($"Attempted to get {nameof(TestsSkipped)} on an uninitialized '{GetType().FullName}' object");
			set => testsSkipped = value;
		}
	}
}
