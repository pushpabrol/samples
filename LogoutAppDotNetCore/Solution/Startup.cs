using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleMvcApp.Support;
using Auth0.AspNetCore.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace SampleMvcApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureSameSiteNoneCookies();

            services.AddAuth0WebAppAuthentication(options =>
            {
                options.Domain = Configuration["Auth0:Domain"];
                options.ClientId = Configuration["Auth0:ClientId"];
                options.OpenIdConnectEvents = new OpenIdConnectEvents()
                {

                    OnAccessDenied = (context) =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/Home/Index?message=" + context.Response.Body);
                        return Task.CompletedTask;


                    },

                    OnAuthenticationFailed = (AuthenticationFailedContext context) =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/Home/Index?message=" + context.Exception.Message);
                        return Task.CompletedTask;


                    },
                    OnRemoteFailure = (context) =>
                    {
                        context.HandleResponse();
                        var data = context.Failure.Data;

                        string result = "/Home/LoggedOut?message=no_session_no_logout";

                        foreach (object key in data.Keys)
                        {
                            if ((string)data[key] == "consent_required")
                            {
                                result = "/Account/Login?message=consent_required";
                                break;
                            }

                            if ((string)data[key] == "login_required")
                            {
                                result = "/Home/LoggedOut?message=no_session";
                                break;
                            }
                        }
                        context.Response.Redirect(result);

                        return Task.CompletedTask;


                    },
                };
            });

            //services.AddCookiePolicy(opts => {
            //    opts.CheckConsentNeeded = ctx => false;
            //    opts.OnAppendCookie = ctx => {
            //        ctx.CookieOptions.Expires = DateTimeOffset.Now.AddSeconds(5);
            //        ctx.CookieName = "LogoutAppCookie";
            //        ctx.CookieOptions.MaxAge = TimeSpan.FromSeconds(5);
            //    };
            //});

            //services.ConfigureApplicationCookie(options =>
            //{
            //    options.ExpireTimeSpan = TimeSpan.FromSeconds(5);
            //    options.Cookie.Expiration = TimeSpan.FromSeconds(5);
            //    options.Cookie.Name = "LogoutAppCookie";
            //    options.Cookie.MaxAge = options.ExpireTimeSpan; // optional

            //    options.Events.OnSigningIn = async (signinContext) => {
            //        signinContext.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(5);
            //        signinContext.CookieOptions.Expires = DateTimeOffset.Now.AddSeconds(5);
            //    };

            //});

           

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
