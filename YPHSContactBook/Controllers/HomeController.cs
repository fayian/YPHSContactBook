using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YPHSContactBook.Models;

namespace YPHSContactBook.Controllers {
    public class HomeController : Controller {
        private readonly ILogger<HomeController> _logger;

        //encrypt/decrypt key and iv, please change it to ensure sercurity
        private static readonly byte[] AESKey = Encoding.ASCII.GetBytes("10831043yphs1820");
        private static readonly byte[] AESIv = Encoding.ASCII.GetBytes("10831043yphs1820");


        private static string EncryptAES(string text) {
            byte[] sourceBytes = Encoding.UTF8.GetBytes(text);
            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = AESKey;
            aes.IV = AESIv;
            var transform = aes.CreateEncryptor();
            return Convert.ToBase64String(transform.TransformFinalBlock(sourceBytes, 0, sourceBytes.Length));
        }

        private static string DecryptAES(string text) {
            byte[] encryptBytes = Convert.FromBase64String(text);
            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = AESKey;
            aes.IV = AESIv;
            var transform = aes.CreateDecryptor();
            return Encoding.UTF8.GetString(transform.TransformFinalBlock(encryptBytes, 0, encryptBytes.Length));
        }

        public HomeController(ILogger<HomeController> logger) {
            _logger = logger;
        }        

        public IActionResult Index() {
            ViewData["loggedIn"] = HttpContext.Request.Cookies.ContainsKey("LoginToken");
            return View();
        }

        public IActionResult Post() {
            if(HttpContext.Request.Cookies.ContainsKey("LoginToken")) {
                ViewData["loggedIn"] = true;
                return View();
            } else {
                return RedirectToAction("LoginPage");
            }            
        }

        public IActionResult PostContactBook(string title, string content, string externalLink) {
            string loginToken;
            if (HttpContext.Request.Cookies.TryGetValue("LoginToken", out loginToken)) {
                string[] loginData = DecryptAES(loginToken).Split("&");

                HttpHandler schoolContactBookWeb = new HttpHandler("http://www.yphs.tp.edu.tw/tea/tua.aspx");
                schoolContactBookWeb.Login(loginData[0], loginData[1], loginData[2]);
                schoolContactBookWeb = new HttpHandler("http://www.yphs.tp.edu.tw/tea/tua-1.aspx");
                schoolContactBookWeb.PostContactBook(title, content, externalLink);
            } else {
                return RedirectToAction("LoginPage");
            }
            ViewData["loggedIn"] = HttpContext.Request.Cookies.ContainsKey("LoginToken");
            return View("/Views/Home/Index.cshtml");
        }

        public IActionResult LoginPage() {
            ViewData["loggedIn"] = false;
            if (HttpContext.Request.Cookies.ContainsKey("LoginToken")) { //check if the user had logged in and has a cookie in the browser
                ViewData["loggedIn"] = true;
                return View("/Views/Home/Index.cshtml");
            } else {
                ViewData["loginFailed"] = false;                
                return View("/Views/Home/Login.cshtml");
            }
        }

        public IActionResult Login(string account, string password, string classID, bool stayLoggedIn) {
            HttpHandler schoolContactBookWeb = new HttpHandler("http://www.yphs.tp.edu.tw/tea/tua.aspx");
            if (schoolContactBookWeb.Login(account, password, classID)) {  //check if the account/password can log into the school web                
                //set login cookie
                CookieOptions option = new CookieOptions { HttpOnly = true };
                if (stayLoggedIn) option.Expires = DateTime.UtcNow.AddYears(1);
                HttpContext.Response.Cookies.Append("LoginToken", EncryptAES(account + "&" + password + "&" + classID), option);
                ViewData["loggedIn"] = true;
                return View("/Views/Home/Index.cshtml");
            } else {
                ViewData["loginFailed"] = true;
                return View("/Views/Home/Login.cshtml");
            }
        }

        public IActionResult Logout() {
            HttpContext.Response.Cookies.Delete("LoginToken");
            ViewData["loggedIn"] = false;
            return View("/Views/Home/Index.cshtml");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
