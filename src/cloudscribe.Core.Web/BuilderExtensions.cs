﻿

using cloudscribe.Core.Identity;
using cloudscribe.Core.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder
{
    public static class BuilderExtensions
    {
        public static IApplicationBuilder UseCloudscribeCoreDefaultAuthentication(
           this IApplicationBuilder builder,
           ILoggerFactory loggerFactory,
           MultiTenantOptions multiTenantOptions,
           SiteSettings tenant,
           CookieSecurePolicy applicationCookieSecure = CookieSecurePolicy.SameAsRequest
           )
        {

            var useFolder = !multiTenantOptions.UseRelatedSitesMode
                                        && multiTenantOptions.Mode == cloudscribe.Core.Models.MultiTenantMode.FolderName
                                        && tenant.SiteFolderName.Length > 0;

            var externalCookieOptions = builder.SetupOtherCookies(
                    AuthenticationScheme.External,
                    multiTenantOptions.UseRelatedSitesMode,
                    tenant);
            builder.UseCookieAuthentication(externalCookieOptions);

            var twoFactorRememberMeCookieOptions = builder.SetupOtherCookies(
                AuthenticationScheme.TwoFactorRememberMe,
                multiTenantOptions.UseRelatedSitesMode,
                tenant);
            builder.UseCookieAuthentication(twoFactorRememberMeCookieOptions);

            var twoFactorUserIdCookie = builder.SetupOtherCookies(
                AuthenticationScheme.TwoFactorUserId,
                multiTenantOptions.UseRelatedSitesMode,
                tenant);
            builder.UseCookieAuthentication(twoFactorUserIdCookie);

            //var cookieEvents = new CookieAuthenticationEvents();
            var logger = loggerFactory.CreateLogger<SiteAuthCookieValidator>();
            var cookieValidator = new SiteAuthCookieValidator(logger);
            var appCookieOptions = builder.SetupAppCookie(
                cookieValidator,
                AuthenticationScheme.Application,
                multiTenantOptions.UseRelatedSitesMode,
                tenant,
                CookieSecurePolicy.Always
                );
            builder.UseCookieAuthentication(appCookieOptions);

            // known issue here is if a site is updated to populate the
            // social auth keys, it currently requires a restart so that the middleware gets registered
            // in order for it to work or for the social auth buttons to appear 
            builder.UseSocialAuth(tenant, externalCookieOptions, useFolder);


            return builder;
        }

        public static IApplicationBuilder UseSocialAuth(
            this IApplicationBuilder app,
            SiteSettings site,
            CookieAuthenticationOptions externalCookieOptions,
            bool shouldUseFolder)
        {
            // TODO: will this require a restart if the options are updated in the ui?
            // no just need to clear the tenant cache after updating the settings
            if (!string.IsNullOrWhiteSpace(site.GoogleClientId))
            {
                var googleOptions = new GoogleOptions();
                googleOptions.AuthenticationScheme = "Google";
                googleOptions.SignInScheme = externalCookieOptions.AuthenticationScheme;
                googleOptions.ClientId = site.GoogleClientId;
                googleOptions.ClientSecret = site.GoogleClientSecret;
                if (shouldUseFolder)
                {
                    googleOptions.CallbackPath = "/" + site.SiteFolderName + "/signin-google";
                }

                app.UseGoogleAuthentication(googleOptions);
            }

            if (!string.IsNullOrWhiteSpace(site.FacebookAppId))
            {
                var facebookOptions = new FacebookOptions();
                facebookOptions.AuthenticationScheme = "Facebook";
                facebookOptions.SignInScheme = externalCookieOptions.AuthenticationScheme;
                facebookOptions.AppId = site.FacebookAppId;
                facebookOptions.AppSecret = site.FacebookAppSecret;

                if (shouldUseFolder)
                {
                    facebookOptions.CallbackPath = "/" + site.SiteFolderName + "/signin-facebook";
                }

                app.UseFacebookAuthentication(facebookOptions);
            }

            if (!string.IsNullOrWhiteSpace(site.MicrosoftClientId))
            {
                var microsoftOptions = new MicrosoftAccountOptions();
                microsoftOptions.SignInScheme = externalCookieOptions.AuthenticationScheme;
                microsoftOptions.ClientId = site.MicrosoftClientId;
                microsoftOptions.ClientSecret = site.MicrosoftClientSecret;
                if (shouldUseFolder)
                {
                    microsoftOptions.CallbackPath = "/" + site.SiteFolderName + "/signin-microsoft";
                }

                app.UseMicrosoftAccountAuthentication(microsoftOptions);
            }

            //app.Use()

            //Func<HttpContext, bool> hasTwitterKeys = (HttpContext context) =>
            //{
            //    var tenant = context.GetTenant<SiteSettings>();
            //    if (tenant == null) return false;
            //    if (string.IsNullOrWhiteSpace(tenant.TwitterConsumerKey)) return false;
            //    if (string.IsNullOrWhiteSpace(tenant.TwitterConsumerSecret)) return false;

            //    return true;
            //};

            //app.UseWhen(context => hasTwitterKeys(context), appBuilder =>
            //{
            if (!string.IsNullOrWhiteSpace(site.TwitterConsumerKey))
            {
                var twitterOptions = new TwitterOptions();
                twitterOptions.SignInScheme = externalCookieOptions.AuthenticationScheme;
                twitterOptions.ConsumerKey = site.TwitterConsumerKey;
                twitterOptions.ConsumerSecret = site.TwitterConsumerSecret;

                if (shouldUseFolder)
                {
                    twitterOptions.CallbackPath = "/" + site.SiteFolderName + "/signin-twitter";
                }

                app.UseTwitterAuthentication(twitterOptions);
            }

            //});



            return app;
        }

        public static CookieAuthenticationOptions SetupAppCookie(
            this IApplicationBuilder app,
           SiteAuthCookieValidator siteValidator,
           string scheme,
           bool useRelatedSitesMode,
           SiteSettings tenant,
           CookieSecurePolicy cookieSecure = CookieSecurePolicy.SameAsRequest
           )
        {
            var cookieEvents = new CookieAuthenticationEvents();
            var options = new CookieAuthenticationOptions();
            if (useRelatedSitesMode)
            {
                options.AuthenticationScheme = scheme;
                options.CookieName = scheme;
                options.CookiePath = "/";
            }
            else
            {
                //options.AuthenticationScheme = $"{scheme}-{tenant.SiteFolderName}";
                options.AuthenticationScheme = scheme;
                options.CookieName = $"{scheme}-{tenant.SiteFolderName}";
                options.CookiePath = "/" + tenant.SiteFolderName;
                cookieEvents.OnValidatePrincipal = siteValidator.ValidatePrincipal;
            }

            var tenantPathBase = string.IsNullOrEmpty(tenant.SiteFolderName)
                ? PathString.Empty
                : new PathString("/" + tenant.SiteFolderName);

            options.LoginPath = tenantPathBase + "/account/login";
            options.LogoutPath = tenantPathBase + "/account/logoff";
            options.AccessDeniedPath = tenantPathBase + "/account/accessdenied";

            options.Events = cookieEvents;

            options.AutomaticAuthenticate = true;
            options.AutomaticChallenge = false;

            options.CookieSecure = cookieSecure;

            return options;
        }

        public static CookieAuthenticationOptions SetupOtherCookies(
            this IApplicationBuilder app,
            string scheme,
            bool useRelatedSitesMode,
            SiteSettings tenant,
            CookieSecurePolicy cookieSecure = CookieSecurePolicy.None
            )
        {
            var options = new CookieAuthenticationOptions();
            if (useRelatedSitesMode)
            {
                options.AuthenticationScheme = scheme;
                options.CookieName = scheme;
                options.CookiePath = "/";
            }
            else
            {
                //options.AuthenticationScheme = $"{scheme}-{tenant.SiteFolderName}";
                options.AuthenticationScheme = scheme;
                options.CookieName = $"{scheme}-{tenant.SiteFolderName}";
                options.CookiePath = "/" + tenant.SiteFolderName;
            }

            options.AutomaticAuthenticate = false;

            if (cookieSecure != CookieSecurePolicy.None)
            {
                options.CookieSecure = cookieSecure;
            }

            return options;

        }

        //public static IApplicationBuilder UseWhen(this IApplicationBuilder app
        //    , Func<HttpContext, bool> condition
        //    , Action<IApplicationBuilder> configuration)
        //{
        //    if (app == null)
        //    {
        //        throw new ArgumentNullException(nameof(app));
        //    }

        //    if (condition == null)
        //    {
        //        throw new ArgumentNullException(nameof(condition));
        //    }

        //    if (configuration == null)
        //    {
        //        throw new ArgumentNullException(nameof(configuration));
        //    }

        //    var builder = app.New();
        //    configuration(builder);

        //    return app.Use(next => {
        //        builder.Run(next);

        //        var branch = builder.Build();

        //        return context => {
        //            if (condition(context))
        //            {
        //                return branch(context);
        //            }

        //            return next(context);
        //        };
        //    });
        //}

    }
}
