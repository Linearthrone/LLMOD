using System.Text.RegularExpressions;

namespace HouseVictoria.Services.VirtualEnvironment
{
    public sealed class VictoriaEmbodimentIntents
    {
        public bool WantsWalk { get; init; }
        public bool WantsTouch { get; init; }
        public bool WantsSee { get; init; }
        public string? TouchTarget { get; init; }
    }

    /// <summary>
    /// Lightweight keyword pass over Victoria's reply to infer walk / see / touch actions for Unreal.
    /// </summary>
    public static partial class VictoriaEmbodimentIntentParser
    {
        public static VictoriaEmbodimentIntents Parse(string? assistantText, string? userText = null)
        {
            var text = assistantText ?? string.Empty;
            var combined = string.IsNullOrWhiteSpace(userText) ? text : $"{text} {userText}";

            var wantsWalk = WalkPattern().IsMatch(combined);
            var wantsTouch = TouchPattern().IsMatch(combined);
            var wantsSee = SeePattern().IsMatch(combined);

            string? touchTarget = null;
            var touchMatch = TouchTargetPattern().Match(text);
            if (touchMatch.Success)
                touchTarget = touchMatch.Groups[1].Value.Trim();
            else if (wantsTouch && !string.IsNullOrWhiteSpace(userText))
            {
                var userTouch = TouchTargetPattern().Match(userText);
                if (userTouch.Success)
                    touchTarget = userTouch.Groups[1].Value.Trim();
            }

            if (!string.IsNullOrWhiteSpace(touchTarget) && touchTarget.Length > 80)
                touchTarget = touchTarget[..80];

            return new VictoriaEmbodimentIntents
            {
                WantsWalk = wantsWalk,
                WantsTouch = wantsTouch,
                WantsSee = wantsSee,
                TouchTarget = string.IsNullOrWhiteSpace(touchTarget) ? "nearby" : touchTarget
            };
        }

        [GeneratedRegex(@"\b(walk|go to|head to|move to|wander|stroll|step over|come here|follow me)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex WalkPattern();

        [GeneratedRegex(@"\b(touch|pick up|grab|hold|press|open|interact with|reach for|take the)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TouchPattern();

        [GeneratedRegex(@"\b(look at|see|watch|observe|glance|scan|what do you see|take a look)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex SeePattern();

        [GeneratedRegex(@"(?:touch|pick up|grab|hold|press|open|interact with|reach for|take the)\s+(?:the\s+)?(.+?)(?:\.|,|!|\?|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TouchTargetPattern();
    }
}
