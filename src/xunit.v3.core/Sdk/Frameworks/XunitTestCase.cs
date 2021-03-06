using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Internal;
using Xunit.Runner.v2;
using Xunit.v3;

namespace Xunit.Sdk
{
	/// <summary>
	/// Default implementation of <see cref="IXunitTestCase"/> for xUnit v3 that supports tests decorated with
	/// both <see cref="FactAttribute"/> and <see cref="TheoryAttribute"/>.
	/// </summary>
	[DebuggerDisplay(@"\{ class = {TestMethod.TestClass.Class.Name}, method = {TestMethod.Method.Name}, display = {DisplayName}, skip = {SkipReason} \}")]
	public class XunitTestCase : TestMethodTestCase, IXunitTestCase
	{
		static readonly ConcurrentDictionary<string, IEnumerable<IAttributeInfo>> assemblyTraitAttributeCache = new ConcurrentDictionary<string, IEnumerable<IAttributeInfo>>(StringComparer.OrdinalIgnoreCase);
		static readonly ConcurrentDictionary<string, IEnumerable<IAttributeInfo>> typeTraitAttributeCache = new ConcurrentDictionary<string, IEnumerable<IAttributeInfo>>(StringComparer.OrdinalIgnoreCase);

		string? testAssemblyUniqueID;
		string? testCollectionUniqueID;
		int timeout;

		/// <summary/>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
		public XunitTestCase()
		{
			// No way for us to get access to the message sink on the execution deserialization path, but that should
			// be okay, because we assume all the issues were reported during discovery.
			DiagnosticMessageSink = new _NullMessageSink();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="XunitTestCase"/> class.
		/// </summary>
		/// <param name="testAssemblyUniqueID">The test assembly unique ID.</param>
		/// <param name="testCollectionUniqueID">The test collection unique ID.</param>
		/// <param name="testClassUniqueID">The test class unique ID.</param>
		/// <param name="testMethodUniqueID">The test method unique ID.</param>
		/// <param name="diagnosticMessageSink">The message sink which receives <see cref="_DiagnosticMessage"/> messages.</param>
		/// <param name="defaultMethodDisplay">Default method display to use (when not customized).</param>
		/// <param name="defaultMethodDisplayOptions">Default method display options to use (when not customized).</param>
		/// <param name="testMethod">The test method this test case belongs to.</param>
		/// <param name="testMethodArguments">The arguments for the test method.</param>
		public XunitTestCase(
			string testAssemblyUniqueID,
			string testCollectionUniqueID,
			string? testClassUniqueID,
			string? testMethodUniqueID,
			_IMessageSink diagnosticMessageSink,
			TestMethodDisplay defaultMethodDisplay,
			TestMethodDisplayOptions defaultMethodDisplayOptions,
			ITestMethod testMethod,
			object?[]? testMethodArguments = null)
				: base(defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
		{
			DiagnosticMessageSink = Guard.ArgumentNotNull(nameof(diagnosticMessageSink), diagnosticMessageSink);
			TestAssemblyUniqueID = Guard.ArgumentNotNull(nameof(testAssemblyUniqueID), testAssemblyUniqueID);
			TestCollectionUniqueID = Guard.ArgumentNotNull(nameof(testCollectionUniqueID), testCollectionUniqueID);
			TestClassUniqueID = testClassUniqueID;
			TestMethodUniqueID = testMethodUniqueID;
		}

		/// <summary>
		/// Gets the message sink used to report <see cref="_DiagnosticMessage"/> messages.
		/// </summary>
		protected _IMessageSink DiagnosticMessageSink { get; }

		/// <summary>
		/// Gets the test assembly unique ID.
		/// </summary>
		protected string TestAssemblyUniqueID
		{
			get => testAssemblyUniqueID ?? throw new InvalidOperationException($"Attempted to get {nameof(TestAssemblyUniqueID)} on an uninitialized '{GetType().FullName}' object");
			set => testAssemblyUniqueID = Guard.ArgumentNotNullOrEmpty(nameof(TestAssemblyUniqueID), value);
		}

		/// <summary>
		/// Gets the test class unique ID.
		/// </summary>
		protected string? TestClassUniqueID { get; set; }

		/// <summary>
		/// Gets the test collection unique ID.
		/// </summary>
		protected string TestCollectionUniqueID
		{
			get => testCollectionUniqueID ?? throw new InvalidOperationException($"Attempted to get {nameof(TestCollectionUniqueID)} on an uninitialized '{GetType().FullName}' object");
			set => testCollectionUniqueID = Guard.ArgumentNotNullOrEmpty(nameof(TestCollectionUniqueID), value);
		}

		/// <summary>
		/// Gets the test method unique ID.
		/// </summary>
		protected string? TestMethodUniqueID { get; set; }

		/// <inheritdoc/>
		public int Timeout
		{
			get
			{
				EnsureInitialized();
				return timeout;
			}
			protected set
			{
				EnsureInitialized();
				timeout = value;
			}
		}

		/// <summary>
		/// Gets the display name for the test case. Calls <see cref="TypeUtility.GetDisplayNameWithArguments"/>
		/// with the given base display name (which is itself either derived from <see cref="FactAttribute.DisplayName"/>,
		/// falling back to <see cref="TestMethodTestCase.BaseDisplayName"/>.
		/// </summary>
		/// <param name="factAttribute">The fact attribute the decorated the test case.</param>
		/// <param name="displayName">The base display name from <see cref="TestMethodTestCase.BaseDisplayName"/>.</param>
		/// <returns>The display name for the test case.</returns>
		protected virtual string GetDisplayName(
			IAttributeInfo factAttribute,
			string displayName)
		{
			Guard.ArgumentNotNull(nameof(factAttribute), factAttribute);
			Guard.ArgumentNotNull(nameof(displayName), displayName);

			return TestMethod.Method.GetDisplayNameWithArguments(displayName, TestMethodArguments, MethodGenericTypes);
		}

		/// <summary>
		/// Gets the skip reason for the test case. By default, pulls the skip reason from the
		/// <see cref="FactAttribute.Skip"/> property.
		/// </summary>
		/// <param name="factAttribute">The fact attribute the decorated the test case.</param>
		/// <returns>The skip reason, if skipped; <c>null</c>, otherwise.</returns>
		protected virtual string GetSkipReason(IAttributeInfo factAttribute)
		{
			Guard.ArgumentNotNull(nameof(factAttribute), factAttribute);

			return factAttribute.GetNamedArgument<string>("Skip");
		}

		/// <summary>
		/// Gets the timeout for the test case. By default, pulls the skip reason from the
		/// <see cref="FactAttribute.Timeout"/> property.
		/// </summary>
		/// <param name="factAttribute">The fact attribute the decorated the test case.</param>
		/// <returns>The timeout in milliseconds, if set; 0, if unset.</returns>
		protected virtual int GetTimeout(IAttributeInfo factAttribute)
		{
			Guard.ArgumentNotNull(nameof(factAttribute), factAttribute);

			return factAttribute.GetNamedArgument<int>("Timeout");
		}

		/// <inheritdoc/>
		protected override void Initialize()
		{
			base.Initialize();

			var factAttribute = TestMethod.Method.GetCustomAttributes(typeof(FactAttribute)).First();
			var baseDisplayName = factAttribute.GetNamedArgument<string>("DisplayName") ?? BaseDisplayName;

			DisplayName = GetDisplayName(factAttribute, baseDisplayName);
			SkipReason = GetSkipReason(factAttribute);
			Timeout = GetTimeout(factAttribute);

			foreach (var traitAttribute in GetTraitAttributesData(TestMethod))
			{
				var discovererAttribute = traitAttribute.GetCustomAttributes(typeof(TraitDiscovererAttribute)).FirstOrDefault();
				if (discovererAttribute != null)
				{
					var discoverer = ExtensibilityPointFactory.GetTraitDiscoverer(DiagnosticMessageSink, discovererAttribute);
					if (discoverer != null)
						foreach (var keyValuePair in discoverer.GetTraits(traitAttribute))
							Traits.Add(keyValuePair.Key, keyValuePair.Value);
				}
				else
					DiagnosticMessageSink.OnMessage(new _DiagnosticMessage { Message = $"Trait attribute on '{DisplayName}' did not have [TraitDiscoverer]" });
			}
		}

		static IEnumerable<IAttributeInfo> GetCachedTraitAttributes(IAssemblyInfo assembly)
		{
			Guard.ArgumentNotNull(nameof(assembly), assembly);

			return assemblyTraitAttributeCache.GetOrAdd(assembly.Name, () => assembly.GetCustomAttributes(typeof(ITraitAttribute)));
		}

		static IEnumerable<IAttributeInfo> GetCachedTraitAttributes(ITypeInfo type)
		{
			Guard.ArgumentNotNull(nameof(type), type);

			return typeTraitAttributeCache.GetOrAdd(type.Name, () => type.GetCustomAttributes(typeof(ITraitAttribute)));
		}

		static IEnumerable<IAttributeInfo> GetTraitAttributesData(ITestMethod testMethod)
		{
			Guard.ArgumentNotNull(nameof(testMethod), testMethod);

			return
				GetCachedTraitAttributes(testMethod.TestClass.Class.Assembly)
					.Concat(testMethod.Method.GetCustomAttributes(typeof(ITraitAttribute)))
					.Concat(GetCachedTraitAttributes(testMethod.TestClass.Class));
		}

		/// <inheritdoc/>
		public virtual Task<RunSummary> RunAsync(
			_IMessageSink diagnosticMessageSink,
			IMessageBus messageBus,
			object?[] constructorArguments,
			ExceptionAggregator aggregator,
			CancellationTokenSource cancellationTokenSource)
		{
			Guard.ArgumentNotNull(nameof(diagnosticMessageSink), diagnosticMessageSink);
			Guard.ArgumentNotNull(nameof(messageBus), messageBus);
			Guard.ArgumentNotNull(nameof(constructorArguments), constructorArguments);
			Guard.ArgumentNotNull(nameof(aggregator), aggregator);
			Guard.ArgumentNotNull(nameof(cancellationTokenSource), cancellationTokenSource);

			return new XunitTestCaseRunner(
				TestAssemblyUniqueID,
				TestCollectionUniqueID,
				TestClassUniqueID,
				TestMethodUniqueID,
				this,
				DisplayName,
				SkipReason,
				constructorArguments,
				TestMethodArguments,
				messageBus,
				aggregator,
				cancellationTokenSource
			).RunAsync();
		}

		/// <inheritdoc/>
		public override void Serialize(IXunitSerializationInfo info)
		{
			Guard.ArgumentNotNull(nameof(info), info);

			base.Serialize(info);

			info.AddValue("TestAssemblyUniqueID", TestAssemblyUniqueID);
			info.AddValue("TestCollectionUniqueID", TestCollectionUniqueID);
			info.AddValue("TestClassUniqueID", TestClassUniqueID);
			info.AddValue("TestMethodUniqueID", TestMethodUniqueID);
			info.AddValue("Timeout", Timeout);
		}

		/// <inheritdoc/>
		public override void Deserialize(IXunitSerializationInfo info)
		{
			Guard.ArgumentNotNull(nameof(info), info);

			base.Deserialize(info);

			TestAssemblyUniqueID = info.GetValue<string>("TestAssemblyUniqueID");
			TestCollectionUniqueID = info.GetValue<string>("TestCollectionUniqueID");
			TestClassUniqueID = info.GetValue<string?>("TestClassUniqueID");
			TestMethodUniqueID = info.GetValue<string?>("TestMethodUniqueID");
			Timeout = info.GetValue<int>("Timeout");
		}
	}
}
