using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Saro.FileServer
{
    public class Startup
    {
        private string m_FileDirectoryPath;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDirectoryBrowser();  //开启目录浏览

            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = 10000;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = int.MaxValue;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            m_FileDirectoryPath = Program.s_Config["DirectoryPath"];
            m_FileDirectoryPath = new DirectoryInfo(m_FileDirectoryPath).FullName;

            var span = m_FileDirectoryPath.AsSpan();
            if (span[span.Length - 1] == '\\')
            {
                var newSpan = span.Slice(0, span.Length - 1);
                m_FileDirectoryPath = newSpan.ToString();
            }
            if (!Directory.Exists(m_FileDirectoryPath))
            {
                Directory.CreateDirectory(m_FileDirectoryPath);
                Console.WriteLine($"Create Content Directory: {m_FileDirectoryPath}");
            }
            else
            {
                Console.WriteLine($"Exist Content Directory: {m_FileDirectoryPath}");
            }

            UseStaticFiles(app, m_FileDirectoryPath);
            app.Run(ProcessRequest);
        }

        private async Task ProcessRequest(HttpContext context)
        {
            if (context.Request.HasFormContentType)
            {
                var sb = new StringBuilder(1024);
                var form = context.Request.Form;

                foreach (var item in form.Files)
                {
                    var ret = ValidateFile(item);
                    if (string.IsNullOrEmpty(ret))
                    {
                        var path = Path.Combine(m_FileDirectoryPath, item.FileName);

                        using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
                        {
                            fs.SetLength(0L);
                            await item.CopyToAsync(fs);
                        }

                        sb.AppendLine().AppendFormat("upload sucess. Name: {0} FileName: {1} FileLength: {2}", item.Name, item.FileName, item.Length);
                    }
                    else
                    {
                        sb.AppendLine().AppendLine($"invalid file '{item.FileName}'. reason: {ret}");
                    }
                }

                sb.Append("FilesCount: ").Append(form.Files.Count);
                await context.Response.WriteAsync(sb.ToString());
            }
            else
            {
                // TODO other case
                await context.Response.WriteAsync("Isn't Form ContentType. Ignore.");
            }
        }

        private string ValidateFile(IFormFile item)
        {
            if (string.IsNullOrEmpty(item.FileName)) return "FileName is null or empty";
            if (item.Length <= 0L) return "File Length is less than 0";

            // 创建目录
            var directory = Path.GetDirectoryName(Path.Combine(m_FileDirectoryPath, item.FileName));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // TODO add more func to check

            return null;
        }

        private void UseStaticFiles(IApplicationBuilder app, string filePath)
        {
            var staticfile = new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                FileProvider = new PhysicalFileProvider(filePath),
                DefaultContentType = "application/x-msdownload",
                // 设置MIME类型类型
                ContentTypeProvider = new FileExtensionContentTypeProvider
                {
                    Mappings =
                    {
                        ["*"] = "application/x-msdownload"
                    }
                },
            };

            app.UseDirectoryBrowser(new DirectoryBrowserOptions() { FileProvider = staticfile.FileProvider });
            app.UseStaticFiles(staticfile);
        }
    }
}