﻿using DocumentViewerCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentViewerCore.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Security;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DocumentViewerCore.Controllers
{
    public class HomeController : Controller
    {
        protected string DocumentDirName = "Documents";

        protected readonly IHttpClientFactory _httpClientFactory;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index(string url)
        {
            Page_Load();
            return View();
        }

        public IActionResult Preview()
        {
            ViewBag.Url = HttpContext.Request.Query["url"];
            ViewBag.Source = HttpContext.Request.Query["source"];
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        protected void Page_Load()
        {
            if (!HttpContext.Request.IsPostBack())
            {
                string url = HttpContext.Request.Query["url"];
                if (!string.IsNullOrEmpty(url))
                {
                    if (new Regex(@"(?i)/.*\.[a-zA-Z]{3,}").IsMatch(url))
                    {
                        string extension = url.Substring(url.LastIndexOf('.'));
                        string fileName = url.Substring(url.LastIndexOf('/') + 1);
                        string filePath = Path.Combine(HttpContextHelper.MapPath("\\" + DocumentDirName + "\\"),
                            fileName);
                        string targetConvertDirPath =
                            HttpContextHelper.MapPath(string.Format("\\{0}\\ConvertHtml", DocumentDirName));
                        //目标文件路径
                        var targetConvertFilePath =
                            string.Format("{0}\\ConvertHtml\\{1}.htm", DocumentDirName, fileName);
                        var targetPath = HttpContextHelper.MapPath("\\" + targetConvertFilePath);

                        if (System.IO.File.Exists(HttpContextHelper.MapPath("\\" + targetConvertFilePath)))
                        {
                            #region 如果文件已存在

                            Uri uri = new Uri(HttpContextHelper.Current.Request.GetDisplayUrl());
                            string port = uri.Port == 80 ? string.Empty : ":" + uri.Port;
                            string webUrl = string.Format("{0}://{1}{2}/", uri.Scheme, uri.Host, port);
                            Response.Redirect(string.Format("/Home/Preview?url={0}{1}&source={2}", webUrl,
                                (targetConvertFilePath).Replace("\\", "/").TrimStart('/'), url));

                            #endregion
                        }
                        else
                        {
                            #region 第一步：下载文件

                            try
                            {
                                Uri address = new Uri(url);
                                var client = _httpClientFactory.CreateClient();
                                var stream = client.GetStreamAsync(address);

                                FileDownSave(url, filePath);
                                //return File(stream, "application/vnd.android.package-archive", Path.GetFileName(targetPath));

                                //ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
                                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
                                //using (WebClient webClient = new WebClient())
                                //{
                                //    webClient.DownloadFile(url, filePath);
                                //    //var stream = webClient.OpenRead(address);
                                //    //using (StreamReader sr = new StreamReader(stream))
                                //    //{
                                //    //    var page = sr.ReadToEnd();
                                //    //}
                                //}


                                //using (var webClient = new WebClient())
                                //{
                                //    if (!Directory.Exists(HttpContextHelper.MapPath("\\" + DocumentDirName + "\\")))
                                //    {
                                //        Directory.CreateDirectory(HttpContextHelper.MapPath("\\" + DocumentDirName + "\\"));
                                //    }
                                //    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                                //    webClient.DownloadFile(url, filePath);


                                //}
                            }
                            catch (Exception ex)
                            {
                                ResponseMsg(false, ex.Message.ToString(CultureInfo.InvariantCulture));
                            }

                            #endregion

                            if (System.IO.File.Exists(filePath))
                            {
                                #region  第二步：转换文件

                                string sourcePath = filePath;
                                if (!Directory.Exists(targetConvertDirPath))
                                {
                                    Directory.CreateDirectory(targetConvertDirPath);
                                }

                                if (System.IO.File.Exists(targetPath))
                                {
                                    System.IO.File.Delete(targetPath);
                                }

                                OfficeConverter.ConvertResult result;
                                switch (extension.Replace(".", "").ToLower())
                                {
                                    #region Word转换

                                    case "doc":
                                    case "docx":
                                    case "txt":
                                    case "csv":
                                    case "cs":
                                    case "wps":
                                    case "js":
                                    case "xml":
                                    case "config":
                                        result = OfficeConverter.WordToHtml(sourcePath, targetPath);
                                        break;

                                    #endregion

                                    #region Excel转换

                                    case "xls":
                                    case "xlsx":
                                    case "et":
                                        result = OfficeConverter.ExcelToHtml(sourcePath, targetPath);
                                        break;

                                    #endregion

                                    #region PPT转换

                                    case "ppt":
                                    case "pptx":
                                    case "wpp":
                                    case "dps":

                                        result = OfficeConverter.PptToHtml(sourcePath, targetPath);
                                        break;

                                    #endregion

                                    #region 图片转换

                                    case "jpg":
                                    case "png":
                                    case "ico":
                                    case "gif":
                                    case "bmp":
                                        result = OfficeConverter.ImageToHtml(sourcePath, targetPath);
                                        break;

                                    #endregion

                                    #region 压缩包

                                    case "zip":
                                    case "rar":
                                        result = OfficeConverter.ZipToHtml(sourcePath, targetPath);
                                        break;

                                    #endregion

                                    default:
                                        result = new OfficeConverter.ConvertResult
                                        {
                                            IsSuccess = false,
                                            Message = "该文档类型不支持在线预览！"
                                        };
                                        break;
                                }

                                if (result.IsSuccess)
                                {
                                    Uri uri = new Uri(HttpContextHelper.Current.Request.GetDisplayUrl());
                                    string port = uri.Port == 80 ? string.Empty : ":" + uri.Port;
                                    string webUrl = string.Format("{0}://{1}{2}/", uri.Scheme, uri.Host, port);
                                    Response.Redirect(string.Format("/Home/Preview?url={0}{1}&source={2}", webUrl,
                                        (targetConvertFilePath).Replace("//", "/").TrimStart('/'), url));
                                }
                                else
                                {
                                    ResponseMsg(false, "对不起，" + result.Message);
                                }

                                #endregion
                            }
                            else
                            {
                                ResponseMsg(false, "对不起，文件下载失败，未找到对应文件！");
                            }
                        }
                    }
                    else
                    {
                        ResponseMsg(false, "对不起，文件路径不正确！");
                    }
                }
            }
        }

        protected void ResponseMsg(bool isOk, string msg)
        {
            Response.Clear();
            string msgage = "<span style='font-size:12px;color:" + (isOk ? "green" : "red") + "'>" + msg + "</span>";
            Response.WriteAsync(msgage);
        }

        public string BasePath
        {
            get { return HttpContextHelper.MapPath(HttpContext.Request.Path); }
        }

        /// <summary>
        /// 下载并保存
        /// </summary>
        /// <param name="url">网络路径</param>
        /// <param name="savePath">保存本地的文件夹</param>
        public void FileDownSave(string url, string savePath)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                string[] strArry = url.Split('/');
                //savePath = savePath + "/" + strArry[strArry.Length - 1];
            }

            var httpClient = _httpClientFactory.CreateClient() ?? new HttpClient();

            var t = httpClient.GetByteArrayAsync(url);
            t.Wait();
            Stream responseStream = new MemoryStream(t.Result);
            Stream stream = new FileStream(savePath, FileMode.Create);
            byte[] bArr = new byte[1024];
            int size = responseStream.Read(bArr, 0, bArr.Length);
            while (size > 0)
            {
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, bArr.Length);
            }

            stream.Close();
            responseStream.Close();
        }
    }
}