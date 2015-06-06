using System;
using System.Collections.Generic;
using System.Web;
using Formo;
using Huxley;
using Huxley.ldbServiceReference;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Common;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(NinjectWebCommon), "Start")]
[assembly: WebActivatorEx.ApplicationShutdownMethodAttribute(typeof(NinjectWebCommon), "Stop")]

namespace Huxley
{
    public static class NinjectWebCommon
    {
        private static readonly Bootstrapper Bootstrapper = new Bootstrapper();

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestHttpModule));
            DynamicModuleUtility.RegisterModule(typeof(NinjectHttpModule));
            Bootstrapper.Initialize(CreateKernel);
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
            Bootstrapper.ShutDown();
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            try
            {
                kernel.Bind<Func<IKernel>>().ToMethod(ctx => () => new Bootstrapper().Kernel);
                kernel.Bind<IHttpModule>().To<HttpApplicationInitializationHttpModule>();
                kernel.Bind<ILdbClient>().To<LdbClient>().InRequestScope();
                kernel.Bind<LDBServiceSoapClient>().To<LDBServiceSoapClient>().InRequestScope();
                kernel.Bind<HuxleySettings>()
                    .ToMethod(ctx => new Configuration().Bind<HuxleySettings>())
                    .InSingletonScope();
                // TODO: Make the file path configurable
                kernel.Bind<IEnumerable<CrsRecord>>()
                    .ToMethod(
                        ctx => CrsRecord.GetCrsCodesSync(HttpContext.Current.Server.MapPath("~/RailReferences.csv")))
                    .InSingletonScope();

                return kernel;
            }
            catch
            {
                kernel.Dispose();
                throw;
            }
        }
    }
}