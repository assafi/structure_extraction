﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace IndexIt
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "FilesRoute",
                routeTemplate: "s/{sid}/files/{file}",
                defaults: new
                {
                    controller = "Files",
                    sid = 0,
                    file = RouteParameter.Optional,
                }
            );

            config.Routes.MapHttpRoute(
                name: "RulesRoute",
                routeTemplate: "s/{sid}/files/{file}/rules/{rule}",
                defaults: new
                {
                    controller = "Files",
                    sid = 0,
                    file = RouteParameter.Optional,
                    rule = RouteParameter.Optional,
                }
            );
        }
    }
}
