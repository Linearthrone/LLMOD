using UnrealBuildTool;

public class HouseVictoriaBridge : ModuleRules
{
    public HouseVictoriaBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "Json"
        });
    }
}
