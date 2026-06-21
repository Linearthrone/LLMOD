using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.Services.Communication
{
    /// <summary>
    /// Detects file-delivery intent in chat and extracts deliverable document payloads
    /// from AI responses (avoiding roleplay-only text being saved as files).
    /// </summary>
    public static class FileDeliveryHelper
    {
        private static readonly string[] FileCreationVerbs =
        {
            "create", "write", "save", "generate", "make", "produce", "draft", "export"
        };

        private static readonly Regex SendDocumentPattern = new(
            @"\b(send|give|share|deliver|provide|pass|hand|have|got|need|want)\b.{0,80}\b(me\s+)?(the\s+)?(a\s+)?(file|document|paper|report|research(?:\s+paper)?|markdown)\b|" +
            @"\b(that|the)\s+(research(\s+paper)?|paper|document|file|report)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CreateDocumentPattern = new(
            @"\b(create|write|save|generate|make|produce|draft|export)\b.{0,80}\b(file|document|paper|report|research(?:\s+paper)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RetrievalFolderPattern = new(
            @"\b(put|save|place|drop|move)\b.{0,80}\b(file\s+retrieval|retrieval\s+folder|generated\s*files?)\b|" +
            @"\b(file\s+retrieval|retrieval\s+folder)\b.{0,80}\b(put|save|place|when\s+you\s+(?:finish|done))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ExistingFileInquiryPattern = new(
            @"\b(what|where|which)\b.{0,40}\bfile\b|\b(not|isn't|wasn't|never)\b.{0,40}\b(in|at|reaching)\b.{0,40}\b(file\s+retrieval|retrieval\s+folder|generated)\b|" +
            @"\b(still\s+not\s+there|didn't\s+(?:get|receive)|never\s+got|not\s+reaching|where\s+did\s+you\s+(?:put|save))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FilePathPattern = new(
            @"(?<path>(?:[A-Za-z]:\\|/)[^\s""'<>|]+?\.(?:md|txt|json|csv|html|xml|py|cs|pdf))|" +
            @"(?<relpath>(?:docs|Media|Data|GeneratedFiles)[/\\][^\s""'<>|]+?\.(?:md|txt|json|csv|html|xml|py|cs|pdf))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ExplicitImageRequestPattern = new(
            @"(generate|create|make|send|show)\s+(?:me\s+)?(?:an?\s+)?(image|picture|photo)|" +
            @"\b(draw|paint|sketch|render)\b|" +
            @"\b(picture|photo|image)\s+of\b|" +
            @"\b(send|show)\s+(?:me\s+)?(?:an?\s+)?(picture|photo|image)\b|" +
            @"\bstable diffusion\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CasualVisualIntentPattern = new(
            @"\b(send|show|give|make|create|generate|draw|paint|want|need|get|another|more|again)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AiImageDeliveryClaimPattern = new(
            @"\b(I('ve|\s+have)?\s+(sent|uploaded|attached|delivered|shared)|here('s| is)\s+(your|the|an?))\s+(?:you\s+)?(?:an?\s+)?(image|picture|photo|selfie|portrait)\b|" +
            @"\b(generating|creating|making|drawing|rendering|working on|about to send)\b.{0,60}\b(image|picture|photo|selfie|portrait|drawing)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AiFileDeliveredClaimPattern = new(
            @"\b(I('ve|\s+have)?\s+(sent|saved|created|written|uploaded|delivered|put|placed|added)|here('s| is)\s+(your|the))\s+(?:you\s+)?(?:the\s+)?(file|document|paper|report|markdown)\b|" +
            @"\b(saved|placed|put)\s+(?:it\s+)?(?:in|to|into)\s+(?:the\s+)?(file\s+retrieval|retrieval\s+folder|generated\s*files?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsUserAskingAboutExistingFile(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return ExistingFileInquiryPattern.IsMatch(message.Trim());
        }

        public static bool IsUserRequestingFileCreation(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var m = message.Trim();
            if (IsUserAskingAboutExistingFile(m))
                return false;

            if (SendDocumentPattern.IsMatch(m) || CreateDocumentPattern.IsMatch(m) || RetrievalFolderPattern.IsMatch(m))
                return true;

            if (!m.Contains("file", StringComparison.OrdinalIgnoreCase))
                return false;

            return FileCreationVerbs.Any(verb => m.Contains(verb, StringComparison.OrdinalIgnoreCase));
        }

        public static bool LooksLikePastedRoleplay(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var t = message.TrimStart();
            if (t.StartsWith("(I ", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("(i ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var openParens = message.Count(c => c == '(');
            return openParens >= 3 && message.Contains("(I ", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasExplicitImageGenerationIntent(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var m = message.Trim();
            return m.Contains("draw", StringComparison.OrdinalIgnoreCase)
                || m.Contains("generate image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("generate an image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("generate a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("create image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("create an image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("create a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("make an image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("make a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send me a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send me an image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send me a photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("show me a picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("picture of", StringComparison.OrdinalIgnoreCase)
                || m.Contains("image of", StringComparison.OrdinalIgnoreCase)
                || m.Contains("photo of", StringComparison.OrdinalIgnoreCase)
                || m.Contains("stable diffusion", StringComparison.OrdinalIgnoreCase)
                || ExplicitImageRequestPattern.IsMatch(m);
        }

        public static bool HasCasualVisualImageIntent(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var m = message.Trim();
            var wantsVisual = m.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("drawing", StringComparison.OrdinalIgnoreCase)
                || m.Contains("portrait", StringComparison.OrdinalIgnoreCase)
                || m.Contains("selfie", StringComparison.OrdinalIgnoreCase);
            if (!wantsVisual)
                return false;

            return CasualVisualIntentPattern.IsMatch(m);
        }

        /// <summary>
        /// True when the user message should trigger real image generation (not a text-only roleplay reply).
        /// </summary>
        public static bool ShouldAttemptImageGeneration(string? message, bool isFollowUpWithPriorPrompt = false)
        {
            if (ShouldBlockImageGeneration(message))
                return false;

            if (HasExplicitImageGenerationIntent(message))
                return true;

            if (isFollowUpWithPriorPrompt)
                return true;

            return HasCasualVisualImageIntent(message);
        }

        public static bool AiPromisesOrClaimsImageDelivery(string? aiResponse) =>
            !string.IsNullOrWhiteSpace(aiResponse) && AiImageDeliveryClaimPattern.IsMatch(aiResponse);

        public static bool AiClaimsFileDelivered(string? aiResponse) =>
            !string.IsNullOrWhiteSpace(aiResponse) && AiFileDeliveredClaimPattern.IsMatch(aiResponse);

        public static bool ShouldBlockImageGeneration(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            // Explicit image requests win over incidental "file" / "research paper" wording in the same message.
            if (HasExplicitImageGenerationIntent(message))
                return false;

            if (IsUserRequestingFileCreation(message) || IsUserAskingAboutExistingFile(message))
                return true;

            if (LooksLikePastedRoleplay(message))
                return true;

            var m = message.Trim();
            if (m.Contains("research paper", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("file retrieval", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("filesystem", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("write command", StringComparison.OrdinalIgnoreCase) ||
                m.Contains("saved to", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Casual "image" mentions inside long roleplay (e.g. "image of you seeing my brilliance").
            if (m.Length > 400 &&
                m.Contains("image", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(m, @"\b(draw|paint|sketch|render|generate\s+(?:an?\s+)?(?:image|picture|photo))\b", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static string BuildFileDeliverySystemPrompt(string generatedFilesPath)
        {
            return $"""
                FILE DELIVERY (required when the user asks for a file, document, or research paper):
                - Put the COMPLETE document inside [FILE]filename.ext[/FILE] markers. Keep your personality outside the markers; only the document body goes inside.
                - Supported extensions: .md, .txt, .json, .csv, .html
                - Do NOT claim you sent, saved, or delivered a file unless the full content is inside [FILE]...[/FILE] or you called the save_to_file_retrieval MCP tool.
                - If you have Hermes MCP tools, prefer save_to_file_retrieval(filename, content) for user-facing deliverables.
                - File Retrieval folder on disk: {generatedFilesPath}
                - Never tell the user a file was saved to docs/ or other paths unless you also wrote it via [FILE] markers or save_to_file_retrieval.
                """;
        }

        public static string? ExtractFileNameFromMessage(string message)
        {
            var patterns = new[]
            {
                @"(?:create|save|generate|put|write|send).*?([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))",
                @"\[FILE\]\s*([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))",
                @"([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))",
                @"(?:file|filename|name).*?([a-zA-Z0-9_\-\.]+\.(txt|md|json|csv|xml|html|css|js|py|cs|cpp|h|hpp))"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                    return match.Groups[1].Value;
            }

            return null;
        }

        public static string ExtractFileContent(string response)
        {
            var fileMarkerPattern = @"\[FILE\]\s*(?:[a-zA-Z0-9_\-\.]+\s*)?\n?(.*?)\[/FILE\]";
            var match = Regex.Match(response, fileMarkerPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                var marked = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(marked))
                    return marked;
            }

            var namedMarkerPattern = @"\[FILE\]\s*([a-zA-Z0-9_\-\.]+\.(?:txt|md|json|csv|xml|html|css|js|py|cs))\s*\[/FILE\]\s*\n+(.*)";
            match = Regex.Match(response, namedMarkerPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 2)
            {
                var body = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(body))
                    return body;
            }

            var codeBlockPattern = @"```(?:[a-zA-Z0-9_\-\.]+)?\s*\n(.*?)```";
            match = Regex.Match(response, codeBlockPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                var block = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(block) && !LooksLikeRoleplayOnly(block))
                    return block;
            }

            return string.Empty;
        }

        public static bool HasDeliverableFilePayload(string aiResponse) =>
            !string.IsNullOrWhiteSpace(ExtractFileContent(aiResponse));

        public static bool LooksLikeRoleplayOnly(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return true;

            if (content.Contains("[FILE]", StringComparison.OrdinalIgnoreCase))
                return false;

            if (Regex.IsMatch(content, @"^\s*#{1,6}\s+\w", RegexOptions.Multiline) ||
                Regex.IsMatch(content, @"^\s*\*\*Abstract\*\*", RegexOptions.IgnoreCase | RegexOptions.Multiline) ||
                Regex.IsMatch(content, @"^\s*Abstract\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                return false;
            }

            var lines = content.Split('\n');
            var stageLines = 0;
            var substantiveLines = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith('(') && line.Contains(')'))
                    stageLines++;
                else if (line.StartsWith('#') || line.StartsWith("- **") || line.StartsWith("1."))
                    substantiveLines++;
            }

            return stageLines >= 2 && substantiveLines == 0;
        }

        public static string ResolveDefaultFileName(string userMessage, string aiResponse)
        {
            return ExtractFileNameFromMessage(userMessage)
                   ?? ExtractFileNameFromMessage(aiResponse)
                   ?? InferDocumentFileName(userMessage, aiResponse)
                   ?? $"document_{DateTime.Now:yyyyMMdd_HHmmss}.md";
        }

        public static string FormatFileCreatedResponse(string filePath, string? chatResponse = null)
        {
            var fileName = Path.GetFileName(filePath);
            var confirmation =
                $"✅ File created successfully!\n\n📄 Filename: {fileName}\n📁 Location: File Retrieval\n\nYou can access it by clicking the File Retrieval button (📥) in the top tray.";

            if (string.IsNullOrWhiteSpace(chatResponse))
                return confirmation;

            var trimmed = chatResponse.Trim();
            if (trimmed.Length > 400 || LooksLikeRoleplayOnly(trimmed))
                return confirmation;

            return $"{trimmed}\n\n✅ File created: {fileName}\n📁 Location: File Retrieval";
        }

        public static string FormatFileNotDeliveredWarning(string aiResponse)
        {
            if (aiResponse.Contains("⚠️ No file was saved", StringComparison.OrdinalIgnoreCase))
                return aiResponse;

            return aiResponse.TrimEnd() +
                   "\n\n⚠️ No file was saved — the reply did not include deliverable content. " +
                   "When sending a document, put the full text inside [FILE]filename.md[/FILE] markers " +
                   "or use the save_to_file_retrieval tool.";
        }

        public static async Task<IReadOnlyList<string>> ImportReferencedFilesAsync(
            string text,
            IFileGenerationService fileGenerationService,
            string generatedFilesRoot)
        {
            var imported = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return imported;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in FilePathPattern.Matches(text))
            {
                var path = match.Groups["path"].Success ? match.Groups["path"].Value : match.Groups["relpath"].Value;
                path = path.Trim().TrimEnd('.', ',', ';', '"', '\'');
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(path))
                    continue;

                var resolved = ResolveCandidatePath(path, generatedFilesRoot);
                if (resolved == null || !File.Exists(resolved))
                    continue;

                if (resolved.StartsWith(generatedFilesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    imported.Add(resolved);
                    continue;
                }

                try
                {
                    var copied = await fileGenerationService.ImportExternalFileAsync(resolved).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(copied))
                        imported.Add(copied);
                }
                catch
                {
                    // Best-effort import; ignore individual failures.
                }
            }

            return imported;
        }

        private static string? ResolveCandidatePath(string path, string generatedFilesRoot)
        {
            if (Path.IsPathRooted(path) && File.Exists(path))
                return Path.GetFullPath(path);

            var repoRoot = FindRepoRoot(generatedFilesRoot);
            if (repoRoot == null)
                return null;

            var combined = Path.GetFullPath(Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(combined) ? combined : null;
        }

        private static string? FindRepoRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "HouseVictoria.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }

        private static string? InferDocumentFileName(string userMessage, string aiResponse)
        {
            var combined = $"{userMessage}\n{aiResponse}";
            if (combined.Contains("research paper", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("architecture of alpha", StringComparison.OrdinalIgnoreCase))
            {
                return "research_paper.md";
            }

            if (combined.Contains("report", StringComparison.OrdinalIgnoreCase))
                return $"report_{DateTime.Now:yyyyMMdd_HHmmss}.md";

            return null;
        }
    }
}
