using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace IndexIt
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{doc}",
                defaults: new { controller = "Home", action = "Index",
                    doc = UrlParameter.Optional,
                    p1 = UrlParameter.Optional,
                    p2 = UrlParameter.Optional
                }
            );
        }
    }
}
