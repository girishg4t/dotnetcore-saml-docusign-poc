using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using ITfoxtec.Identity.Saml2.MvcCore.Configuration;
using Lingk_SAML_Example.Controllers;
using YamlDotNet.Serialization;
using System.IO;
using Lingk_SAML_Example.Pages;
using Lingk_SAML_Example.DTO;
using Microsoft.AspNetCore.Http;
using Lingk_SAML_Example.Utils;
using Microsoft.AspNetCore.Rewrite;
using Lingk_SAML_Example.Middleware;
using Lingk_SAML_Example.LingkFileSystem;
using Lingk_SAML_Example.Constants;
using Lingk_SAML_Example.Libs;
using Microsoft.Extensions.Options;

namespace Lingk_SAML_Example
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            if (Configuration["ASPNETCORE_YAML_CONFIG"] == null)
            {
                throw new Exception("ASPNETCORE_YAML_CONFIG need to be passed");
            }
            var reader = new StreamReader(Configuration["ASPNETCORE_YAML_CONFIG"]);
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize(reader);

            var serializer = new SerializerBuilder()
                .JsonCompatible()
                .Build();

            var json = serializer.Serialize(yamlObject);
            LingkFile.Create(LingkConst.TempSettingsPath, json);
            var builder = new ConfigurationBuilder().AddJsonFile(LingkConst.TempSettingsPath);
            this.Configuration = builder.Build();
            var lingkConfig = new LingkConfig();
            Configuration.Bind(lingkConfig);
            LingkYaml.LingkYamlConfig = lingkConfig;
            File.Delete(LingkConst.TempSettingsPath);
            LingkFile.Create(LingkConst.LingkFileSystemPath, "");
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.Configure<LingkConfig>(Configuration);

            services.Configure<Saml2Configuration>(saml2Configuration =>
            {
                saml2Configuration.AllowedAudienceUris.Add(this.Configuration["authn:saml:issuerId"]);

                var entityDescriptor = new EntityDescriptor();
                entityDescriptor.ReadIdPSsoDescriptorFromFile(this.Configuration["authn:saml:metadataLocal"]);
                if (entityDescriptor.IdPSsoDescriptor != null)
                {
                    saml2Configuration.SingleSignOnDestination = entityDescriptor.IdPSsoDescriptor.SingleSignOnServices.First().Location;
                    saml2Configuration.SignatureValidationCertificates.AddRange(entityDescriptor.IdPSsoDescriptor.SigningCertificates);
                }
                else
                {
                    throw new Exception("IdPSsoDescriptor not loaded from metadata.");
                }
            });
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            services.AddSaml2();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            //enable session before MVC
            app.UseSession();
            app.UseRouting();
            app.UseSaml2();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
            app.UseMiddleware<RedirecterMiddleware>();
            var options = new RewriteOptions()
            .AddRedirect("(.*)", "auth/login");

            app.UseRewriter(options);
        }

    }

}
