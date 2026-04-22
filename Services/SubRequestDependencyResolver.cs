using MSFSSHTTP.Models;

namespace MSFSSHTTP.Services
{
    /// <summary>
    /// Resolves sub-request dependency graphs per MS-FSSHTTP Section 3.1.4.1 and Section 2.2.5.3.
    ///
    /// The protocol server uses DependsOn and DependencyType attributes to decide whether
    /// a sub-request should be executed. Sub-requests are topologically sorted so that
    /// parents are always processed before their dependents.
    /// </summary>
    public class SubRequestDependencyResolver
    {
        /// <summary>
        /// Result of evaluating a dependency condition.
        /// </summary>
        public class DependencyResult
        {
            public bool ShouldExecute { get; }
            public string ErrorCode { get; }
            public string HResult { get; }

            private DependencyResult(bool shouldExecute, string errorCode = null, string hResult = null)
            {
                ShouldExecute = shouldExecute;
                ErrorCode = errorCode;
                HResult = hResult;
            }

            public static DependencyResult Execute() => new(true);
            public static DependencyResult Skip(string errorCode) => new(false, errorCode, "2147500037");
        }

        /// <summary>
        /// Topologically sorts sub-requests so that parents (DependsOn targets) are processed
        /// before their dependents. Sub-requests with no dependencies come first.
        /// </summary>
        public static List<SubRequestElementGenericType> TopologicalSort(SubRequestElementGenericType[] subRequests)
        {
            if (subRequests == null || subRequests.Length == 0)
                return new List<SubRequestElementGenericType>();

            var tokenToRequest = new Dictionary<string, SubRequestElementGenericType>();
            var inDegree = new Dictionary<string, int>();
            var adjacency = new Dictionary<string, List<string>>(); // parent -> children

            foreach (var sr in subRequests)
            {
                tokenToRequest[sr.SubRequestToken] = sr;
                inDegree[sr.SubRequestToken] = 0;
                adjacency[sr.SubRequestToken] = new List<string>();
            }

            foreach (var sr in subRequests)
            {
                if (!string.IsNullOrEmpty(sr.DependsOn) && tokenToRequest.ContainsKey(sr.DependsOn))
                {
                    adjacency[sr.DependsOn].Add(sr.SubRequestToken);
                    inDegree[sr.SubRequestToken]++;
                }
            }

            // Kahn's algorithm
            var queue = new Queue<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var sorted = new List<SubRequestElementGenericType>();
            while (queue.Count > 0)
            {
                var token = queue.Dequeue();
                sorted.Add(tokenToRequest[token]);

                foreach (var child in adjacency[token])
                {
                    inDegree[child]--;
                    if (inDegree[child] == 0)
                        queue.Enqueue(child);
                }
            }

            // If cycle detected (sorted.Count < subRequests.Length), add remaining in original order
            if (sorted.Count < subRequests.Length)
            {
                var sortedTokens = new HashSet<string>(sorted.Select(s => s.SubRequestToken));
                foreach (var sr in subRequests)
                {
                    if (!sortedTokens.Contains(sr.SubRequestToken))
                        sorted.Add(sr);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Evaluates whether a dependent sub-request should be executed based on its
        /// DependencyType and the parent sub-response's ErrorCode.
        ///
        /// Per MS-FSSHTTP Section 2.2.5.3:
        /// - OnExecute: Process if parent was executed (not skipped due to its own dependency failure)
        /// - OnSuccess: Process only if parent ErrorCode == "Success"
        /// - OnFail: Process only if parent ErrorCode != "Success"
        /// - OnNotSupported: Process only if parent returned a not-supported error
        /// - OnSuccessOrNotSupported: Process if parent succeeded or was not supported
        /// </summary>
        public static DependencyResult EvaluateDependency(
            DependencyTypes dependencyType,
            SubResponseElementGenericType parentResponse,
            bool parentWasSkippedByDependency)
        {
            if (parentResponse == null)
            {
                return DependencyResult.Skip(
                    DependencyCheckRelatedErrorCodeTypes.DependentRequestNotExecuted.ToString());
            }

            var parentErrorCode = parentResponse.ErrorCode;
            bool parentSucceeded = parentErrorCode == GenericErrorCodeTypes.Success.ToString();
            bool parentNotSupported = IsNotSupportedError(parentErrorCode);

            switch (dependencyType)
            {
                case DependencyTypes.OnExecute:
                    // Per spec: "dependency succeeds if RequestA succeeds OR
                    // (RequestA failed NOT because an OnSuccess dependency failed
                    // AND RequestA failed NOT because an OnFail dependency failed
                    // AND RequestA failed NOT because an OnExecute dependency failed)"
                    if (parentSucceeded || !parentWasSkippedByDependency)
                        return DependencyResult.Execute();
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.DependentRequestNotExecuted.ToString());

                case DependencyTypes.OnSuccess:
                    if (parentSucceeded)
                        return DependencyResult.Execute();
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.DependentOnlyOnSuccessRequestFailed.ToString());

                case DependencyTypes.OnFail:
                    if (!parentSucceeded)
                        return DependencyResult.Execute();
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.DependentOnlyOnFailRequestSucceeded.ToString());

                case DependencyTypes.OnNotSupported:
                    if (parentNotSupported)
                        return DependencyResult.Execute();
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.DependentOnlyOnNotSupportedRequestGetSupported.ToString());

                case DependencyTypes.OnSuccessOrNotSupported:
                    if (parentSucceeded || parentNotSupported)
                        return DependencyResult.Execute();
                    // Per spec section 2.2.5.2: when parent failed and DependencyType is
                    // OnSuccess or OnSuccessOrNotSupported, return DependentOnlyOnSuccessRequestFailed
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.DependentOnlyOnSuccessRequestFailed.ToString());

                default:
                    return DependencyResult.Skip(
                        DependencyCheckRelatedErrorCodeTypes.InvalidRequestDependencyType.ToString());
            }
        }

        /// <summary>
        /// Checks if the error code indicates the parent sub-request type is not supported.
        /// </summary>
        private static bool IsNotSupportedError(string errorCode)
        {
            return errorCode == GenericErrorCodeTypes.DependentOnlyOnNotSupportedRequestGetSupported.ToString()
                || errorCode == GenericErrorCodeTypes.RequestNotSupported.ToString()
                || errorCode == GenericErrorCodeTypes.FeatureDisabledOnServerTenantNotSupported.ToString();
        }
    }
}
