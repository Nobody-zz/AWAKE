using MarcusAIFramework.Api;
using TaleWorlds.MountAndBlade;

namespace YourCompany.YourExtension
{
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            FrameworkHostLocator.Register(new TemplateExtension());
        }
    }
}
