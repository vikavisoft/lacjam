using Castle.Windsor;
using Lacjam.Framework.Installer;

namespace Lacjam.Core.Infrastructure.Ioc
{
    public class IocContainer : WindsorContainer
    {
        public IocContainer()
        {
            Install(new FrameworkInstaller());
            Install(new CoreInstaller());
        }
    }
}
