using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Internal;
using Xunit.v3;

namespace Xunit.Sdk
{
	/// <summary>
	/// A base class that provides default behavior when running tests in a test method.
	/// </summary>
	/// <typeparam name="TTestCase">The type of the test case used by the test framework. Must
	/// derive from <see cref="ITestCase"/>.</typeparam>
	public abstract class TestMethodRunner<TTestCase>
		where TTestCase : ITestCase
	{
		ExceptionAggregator aggregator;
		CancellationTokenSource cancellationTokenSource;
		IReflectionTypeInfo @class;
		IMessageBus messageBus;
		IReflectionMethodInfo method;
		IEnumerable<TTestCase> testCases;
		ITestMethod testMethod;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestMethodRunner{TTestCase}"/> class.
		/// </summary>
		/// <param name="testAssemblyUniqueID">The test assembly unique ID.</param>
		/// <param name="testCollectionUniqueID">The test collection unique ID.</param>
		/// <param name="testClassUniqueID">The test class unique ID.</param>
		/// <param name="testMethod">The test method under test.</param>
		/// <param name="class">The CLR class that contains the test method.</param>
		/// <param name="method">The CLR method that contains the tests to be run.</param>
		/// <param name="testCases">The test cases to be run.</param>
		/// <param name="messageBus">The message bus to report run status to.</param>
		/// <param name="aggregator">The exception aggregator used to run code and collect exceptions.</param>
		/// <param name="cancellationTokenSource">The task cancellation token source, used to cancel the test run.</param>
		protected TestMethodRunner(
			string testAssemblyUniqueID,
			string testCollectionUniqueID,
			string? testClassUniqueID,
			ITestMethod testMethod,
			IReflectionTypeInfo @class,
			IReflectionMethodInfo method,
			IEnumerable<TTestCase> testCases,
			IMessageBus messageBus,
			ExceptionAggregator aggregator,
			CancellationTokenSource cancellationTokenSource)
		{
			this.testMethod = Guard.ArgumentNotNull(nameof(testMethod), testMethod);
			this.@class = Guard.ArgumentNotNull(nameof(@class), @class);
			this.method = Guard.ArgumentNotNull(nameof(method), method);
			this.testCases = Guard.ArgumentNotNull(nameof(testCases), testCases);
			this.messageBus = Guard.ArgumentNotNull(nameof(messageBus), messageBus);
			this.aggregator = Guard.ArgumentNotNull(nameof(aggregator), aggregator);
			this.cancellationTokenSource = Guard.ArgumentNotNull(nameof(cancellationTokenSource), cancellationTokenSource);

			TestAssemblyUniqueID = testAssemblyUniqueID;
			TestCollectionUniqueID = testCollectionUniqueID;
			TestClassUniqueID = testClassUniqueID;
			TestMethodUniqueID = UniqueIDGenerator.ForTestMethod(TestClassUniqueID, testMethod.Method?.Name);
		}

		/// <summary>
		/// Gets or sets the exception aggregator used to run code and collect exceptions.
		/// </summary>
		protected ExceptionAggregator Aggregator
		{
			get => aggregator;
			set => aggregator = Guard.ArgumentNotNull(nameof(Aggregator), value);
		}

		/// <summary>
		/// Gets or sets the task cancellation token source, used to cancel the test run.
		/// </summary>
		protected CancellationTokenSource CancellationTokenSource
		{
			get => cancellationTokenSource;
			set => cancellationTokenSource = Guard.ArgumentNotNull(nameof(CancellationTokenSource), value);
		}

		/// <summary>
		/// Gets or sets the CLR class that contains the test method.
		/// </summary>
		protected IReflectionTypeInfo Class
		{
			get => @class;
			set => @class = Guard.ArgumentNotNull(nameof(Class), value);
		}

		/// <summary>
		/// Gets or sets the message bus to report run status to.
		/// </summary>
		protected IMessageBus MessageBus
		{
			get => messageBus;
			set => messageBus = Guard.ArgumentNotNull(nameof(MessageBus), value);
		}

		/// <summary>
		/// Gets or sets the CLR method that contains the tests to be run.
		/// </summary>
		protected IReflectionMethodInfo Method
		{
			get => method;
			set => method = Guard.ArgumentNotNull(nameof(Method), value);
		}

		/// <summary>
		/// Gets or sets the test cases to be run.
		/// </summary>
		protected IEnumerable<TTestCase> TestCases
		{
			get => testCases;
			set => testCases = Guard.ArgumentNotNull(nameof(TestCases), value);
		}

		/// <summary>
		/// Gets or sets the test method that contains the test cases.
		/// </summary>
		protected ITestMethod TestMethod
		{
			get => testMethod;
			set => testMethod = Guard.ArgumentNotNull(nameof(TestMethod), value);
		}

		/// <summary>
		/// Gets the test assembly unique ID.
		/// </summary>
		protected string TestAssemblyUniqueID { get; }

		/// <summary>
		/// Gets the test class unique ID.
		/// </summary>
		protected string? TestClassUniqueID { get; }

		/// <summary>
		/// Gets the test collection unique ID.
		/// </summary>
		protected string TestCollectionUniqueID { get; }

		/// <summary>
		/// Gets the test method unique ID.
		/// </summary>
		protected string? TestMethodUniqueID { get; }

		/// <summary>
		/// This method is called just after <see cref="_TestMethodStarting"/> is sent, but before any test cases are run.
		/// This method should NEVER throw; any exceptions should be placed into the <see cref="Aggregator"/>.
		/// </summary>
		protected virtual void AfterTestMethodStarting()
		{ }

		/// <summary>
		/// This method is called just before <see cref="_TestMethodFinished"/> is sent.
		/// This method should NEVER throw; any exceptions should be placed into the <see cref="Aggregator"/>.
		/// </summary>
		protected virtual void BeforeTestMethodFinished()
		{ }

		/// <summary>
		/// Runs the tests in the test method.
		/// </summary>
		/// <returns>Returns summary information about the tests that were run.</returns>
		public async Task<RunSummary> RunAsync()
		{
			var methodSummary = new RunSummary();

			var methodStarting = new _TestMethodStarting
			{
				AssemblyUniqueID = TestAssemblyUniqueID,
				TestClassUniqueID = TestClassUniqueID,
				TestCollectionUniqueID = TestCollectionUniqueID,
				TestMethod = TestMethod.Method.Name,
				TestMethodUniqueID = TestMethodUniqueID
			};
			if (!MessageBus.QueueMessage(methodStarting))
				CancellationTokenSource.Cancel();
			else
			{
				try
				{
					AfterTestMethodStarting();
					methodSummary = await RunTestCasesAsync();

					Aggregator.Clear();
					BeforeTestMethodFinished();

					if (Aggregator.HasExceptions)
					{
						var methodCleanupFailure = _TestMethodCleanupFailure.FromException(Aggregator.ToException()!, TestAssemblyUniqueID, TestCollectionUniqueID, TestClassUniqueID, TestMethodUniqueID);
						if (!MessageBus.QueueMessage(methodCleanupFailure))
							CancellationTokenSource.Cancel();
					}
				}
				finally
				{
					var testMethodFinished = new _TestMethodFinished
					{
						AssemblyUniqueID = TestAssemblyUniqueID,
						ExecutionTime = methodSummary.Time,
						TestClassUniqueID = TestClassUniqueID,
						TestCollectionUniqueID = TestCollectionUniqueID,
						TestMethodUniqueID = TestMethodUniqueID,
						TestsFailed = methodSummary.Failed,
						TestsRun = methodSummary.Total,
						TestsSkipped = methodSummary.Skipped
					};

					if (!MessageBus.QueueMessage(testMethodFinished))
						CancellationTokenSource.Cancel();

				}
			}

			return methodSummary;
		}

		/// <summary>
		/// Runs the list of test cases. By default, it runs the cases in order, synchronously.
		/// </summary>
		/// <returns>Returns summary information about the tests that were run.</returns>
		protected virtual async Task<RunSummary> RunTestCasesAsync()
		{
			var summary = new RunSummary();

			foreach (var testCase in TestCases)
			{
				summary.Aggregate(await RunTestCaseAsync(testCase));
				if (CancellationTokenSource.IsCancellationRequested)
					break;
			}

			return summary;
		}

		/// <summary>
		/// Override this method to run an individual test case.
		/// </summary>
		/// <param name="testCase">The test case to be run.</param>
		/// <returns>Returns summary information about the test case run.</returns>
		protected abstract Task<RunSummary> RunTestCaseAsync(TTestCase testCase);
	}
}
