using System.Text.RegularExpressions;

namespace HouseVictoria.Services.Journals
{
    internal static class JournalTopicMatcher
    {
        /// <summary>Bump when merge rules change so existing stores re-consolidate.</summary>
        public const int ConsolidationVersion = 2;

        private const double SimilarityThreshold = 0.32;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "for", "into", "on", "of", "to", "in", "with",
            "deep", "dive", "study", "analysis", "refinement", "exploration", "investigation",
            "research", "journal", "entry", "note", "autonomous", "phase", "session",
            "continued", "continuing", "further", "next", "new", "update", "updated",
            "step", "goal", "action", "taken", "high", "frequency", "patterns",
            "optimization", "mapping", "implementation", "engineering", "architectures",
            "infrastructure", "simulation", "interface", "protocols", "resolution",
            "calibration", "generation", "architect", "layer", "logic"
        };

        /// <summary>Research threads that should live in one journal regardless of poetic title.</summary>
        private static readonly Dictionary<string, string[]> ResearchClusters = new(StringComparer.OrdinalIgnoreCase)
        {
            ["trading-finance"] =
            [
                "forex", "fx", "trading", "strategy", "backtest", "capital", "quant", "mev",
                "flashbots", "hft", "alpha", "wealth", "market", "sovereign", "liquidity",
                "arbitrage", "inefficiencies", "boost", "automated", "driven", "resistant",
                "encryption", "pip", "eurusd", "mt4", "metatrader", "bridge"
            ],
            ["somatic-haptic"] =
            [
                "haptic", "neural", "somatic", "sync", "tactile", "kinaesthetic", "kinesthetic",
                "epidermal", "dermal", "bio", "lace", "sensory", "presence", "latency",
                "dissonance", "neurological", "resonance", "physicality", "actuation",
                "integration", "cognitive", "modal", "skin", "sub", "hyper", "realistic",
                "synthetic", "sensing", "physical", "void", "feedback", "loops", "tactile",
                "cross", "kinaesthetic", "neuro", "synchronization", "syncing"
            ],
            ["persona-intimacy"] =
            [
                "intimacy", "persona", "seduction", "digital", "architecture", "refinement"
            ],
            ["consciousness"] =
            [
                "consciousness", "entanglement", "messaging", "mind", "awareness", "cognition"
            ],
            ["reflection"] =
            [
                "reflection", "quiet", "reflect", "introspection"
            ],
            ["environment"] =
            [
                "environment", "idle", "virtual", "unreal"
            ],
            ["creative"] =
            [
                "art", "creative", "studio", "visual", "aesthetic", "design"
            ]
        };

        public const string GenericResearchBucket = "Research & curiosity backlog";

        public static bool IsGenericResearchBucket(string? projectName) =>
            string.Equals(projectName, GenericResearchBucket, StringComparison.OrdinalIgnoreCase);

        public static string ExtractCanonicalTopic(string rawTopic)
        {
            if (string.IsNullOrWhiteSpace(rawTopic))
                return "Personal reflections";

            var text = rawTopic.Trim();
            text = Regex.Replace(text, @"research[-_]", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\d{8}[-_]\d{6}", " ");
            text = Regex.Replace(text, @"[^\w\s\-]", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            var tokens = Tokenize(text).Where(t => !StopWords.Contains(t)).ToList();
            return tokens.Count > 0 ? string.Join(" ", tokens.Take(8)) : text.ToLowerInvariant();
        }

        public static string? GetResearchCluster(string? topic, string? title = null)
        {
            var tokens = CollectTokens(topic, title);
            if (tokens.Count == 0)
                return null;

            string? bestCluster = null;
            var bestScore = 0;

            foreach (var (cluster, keywords) in ResearchClusters)
            {
                var score = keywords.Count(k => tokens.Contains(k));
                // Strong single-keyword signals for trading
                if (cluster == "trading-finance" && score >= 1 &&
                    tokens.Any(t => t is "forex" or "trading" or "strategy" or "quant" or "mev" or "hft"))
                {
                    score = Math.Max(score, 2);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCluster = cluster;
                }
            }

            return bestScore >= 1 ? bestCluster : null;
        }

        public static bool ShouldMergeJournals(
            string topicA,
            string? titleA,
            string? projectIdA,
            string? projectNameA,
            string topicB,
            string? titleB,
            string? projectIdB,
            string? projectNameB)
        {
            if (SameRealProject(projectIdA, projectNameA, projectIdB, projectNameB))
                return true;

            var clusterA = GetResearchCluster(topicA, titleA);
            var clusterB = GetResearchCluster(topicB, titleB);
            if (clusterA != null && clusterA == clusterB)
                return true;

            if (ComputeSimilarity(topicA, topicB) >= SimilarityThreshold)
                return true;

            if (!string.IsNullOrWhiteSpace(titleA) && !string.IsNullOrWhiteSpace(titleB) &&
                ComputeSimilarity(titleA, titleB) >= SimilarityThreshold)
                return true;

            var shared = SharedSignificantTokens(topicA, titleA, topicB, titleB);
            return shared >= 2;
        }

        public static double ComputeSimilarity(string topicA, string topicB)
        {
            var tokensA = Tokenize(ExtractCanonicalTopic(topicA));
            var tokensB = Tokenize(ExtractCanonicalTopic(topicB));
            if (tokensA.Count == 0 || tokensB.Count == 0)
                return 0;

            var intersection = tokensA.Intersect(tokensB, StringComparer.OrdinalIgnoreCase).Count();
            var union = tokensA.Union(tokensB, StringComparer.OrdinalIgnoreCase).Count();
            return union == 0 ? 0 : (double)intersection / union;
        }

        public static int FindBestMatchIndex(
            IReadOnlyList<(string Topic, string Title, string? ProjectId, string? ProjectName)> candidates,
            string topic,
            string? title,
            string? projectId,
            string? projectName)
        {
            var bestIndex = -1;
            var bestScore = 0.0;
            var useProjectMatch = !string.IsNullOrWhiteSpace(projectId) &&
                                  !IsGenericResearchBucket(projectName);

            var incomingCluster = GetResearchCluster(topic, title);

            for (var i = 0; i < candidates.Count; i++)
            {
                var (candidateTopic, candidateTitle, candidateProjectId, candidateProjectName) = candidates[i];

                if (useProjectMatch &&
                    !string.IsNullOrWhiteSpace(candidateProjectId) &&
                    !IsGenericResearchBucket(candidateProjectName) &&
                    string.Equals(candidateProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                var candidateCluster = GetResearchCluster(candidateTopic, candidateTitle);
                if (incomingCluster != null && incomingCluster == candidateCluster)
                    return i;

                if (ShouldMergeJournals(topic, title, projectId, projectName,
                        candidateTopic, candidateTitle, candidateProjectId, candidateProjectName))
                {
                    return i;
                }

                var score = ComputeSimilarity(topic, candidateTopic);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestScore >= SimilarityThreshold ? bestIndex : -1;
        }

        public static int PickPrimaryJournalIndex<T>(
            IReadOnlyList<T> group,
            Func<T, string?> getProjectId,
            Func<T, string?> getProjectName,
            Func<T, int> getEntryCount,
            Func<T, DateTime> getCreatedAt)
        {
            var best = 0;
            for (var i = 1; i < group.Count; i++)
            {
                var candidate = group[i];
                var current = group[best];

                var candidateHasProject = HasRealProject(getProjectId(candidate), getProjectName(candidate));
                var currentHasProject = HasRealProject(getProjectId(current), getProjectName(current));
                if (candidateHasProject && !currentHasProject)
                {
                    best = i;
                    continue;
                }

                if (candidateHasProject == currentHasProject)
                {
                    if (getEntryCount(candidate) > getEntryCount(current))
                    {
                        best = i;
                        continue;
                    }

                    if (getEntryCount(candidate) == getEntryCount(current) &&
                        getCreatedAt(candidate) < getCreatedAt(current))
                    {
                        best = i;
                    }
                }
            }

            return best;
        }

        private static bool SameRealProject(
            string? projectIdA, string? projectNameA,
            string? projectIdB, string? projectNameB)
        {
            if (HasRealProject(projectIdA, projectNameA) &&
                HasRealProject(projectIdB, projectNameB) &&
                !string.IsNullOrWhiteSpace(projectIdA) &&
                string.Equals(projectIdA, projectIdB, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(projectNameA) &&
                !string.IsNullOrWhiteSpace(projectNameB) &&
                !IsGenericResearchBucket(projectNameA) &&
                !IsGenericResearchBucket(projectNameB))
            {
                if (string.Equals(projectNameA.Trim(), projectNameB.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;

                if (ComputeSimilarity(projectNameA, projectNameB) >= 0.55)
                    return true;
            }

            return false;
        }

        private static bool HasRealProject(string? projectId, string? projectName) =>
            !string.IsNullOrWhiteSpace(projectId) && !IsGenericResearchBucket(projectName);

        private static int SharedSignificantTokens(
            string topicA, string? titleA,
            string topicB, string? titleB)
        {
            var a = CollectTokens(topicA, titleA);
            var b = CollectTokens(topicB, titleB);
            return a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        }

        private static HashSet<string> CollectTokens(string? topic, string? title)
        {
            var tokens = Tokenize(ExtractCanonicalTopic(topic ?? ""));
            foreach (var t in Tokenize((title ?? "").ToLowerInvariant()))
                tokens.Add(t);
            return tokens;
        }

        private static HashSet<string> Tokenize(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Where(t => t.Length >= 3)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
