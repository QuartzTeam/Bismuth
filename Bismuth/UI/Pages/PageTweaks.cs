namespace Bismuth.UI.Pages
{
    // "Tweaks" tab — small game-behaviour overrides (editor helpers live on the Editor tab).
    internal static class PageTweaks
    {
        public static void Build(PageStack stack)
        {
            var content = stack.Root;
            var s = UICore.Settings;
            var notify = UICore.OnSettingsChanged;

            UIBuilder.SectionHeader(content, "Custom levels");
            UIBuilder.Slider(content, "Preview volume %", s.ClsPreviewVolume * 100f, 0f, 100f,
                v =>
                {
                    s.ClsPreviewVolume = v / 100f;
                    Tweaks.ApplyClsPreviewVolume();
                    notify?.Invoke();
                }, "0", 1f);
        }
    }
}
