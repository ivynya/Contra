﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OpenTalon.Areas.Identity.Data;
using OpenTalon.Data;
using OpenTalon.Models;
using Microsoft.AspNetCore.Routing;

namespace OpenTalon.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<OpenTalonUser> _userManager;

        public HomeController(ApplicationDbContext context,
                              UserManager<OpenTalonUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            List<List<Article>> articles = new List<List<Article>>();
            Article placeholder = new Article
            {
                Title = "Relevant Story",
                SummaryLong = "Oops! Looks like we don't have enough articles to show you right now.",
                Id = -1
            };

            Random rnd = new Random();
            List<Article> top = (from a in _context.Article
                                 where a.Approved == ApprovalStatus.Approved
                                 orderby a.Date descending
                                 select a).ToList();
            while (top.Count < 4)
            {
                placeholder.ThumbnailURL = "../img/img0" + rnd.Next(1, 5).ToString() + ".jpg";
                top.Add(placeholder);
            }
            articles.Add(top);

            List<Article> editorial = (from a in _context.Article
                                       where a.Approved == ApprovalStatus.Approved && 
                                       a.SummaryShort.Contains("Featured") &&
                                       a.SummaryShort.Contains("Editorial")
                                       orderby a.Date descending
                                       select a).ToList();
            while (editorial.Count < 4)
            {
                placeholder.ThumbnailURL = "../img/img0" + rnd.Next(1, 5).ToString() + ".jpg";
                editorial.Add(placeholder);
            }
            articles.Add(editorial);

            List<Article> newsbeat = (from a in _context.Article
                                      where a.Approved == ApprovalStatus.Approved &&
                                      a.SummaryShort.Contains("Newsbeat") ||
                                      a.SummaryShort.Contains("Event")
                                      orderby a.Date descending
                                      select a).ToList();
            while (newsbeat.Count < 8)
            {
                placeholder.ThumbnailURL = "../img/img0" + rnd.Next(1, 5).ToString() + ".jpg";
                newsbeat.Add(placeholder);
            }
            articles.Add(newsbeat);

            return View(articles);
        }

        [Route("/about")]
        public IActionResult About()
        {
            return View(_userManager.GetUsersInRoleAsync("Staff").Result);
        }

        [Route("/article/{*id}")]
        public async Task<IActionResult> Article(int? id)
        {
            if (id == null) return Redirect("~/404");

            var article = await _context.Article.FirstOrDefaultAsync(m => m.Id == id);
            if (article == null) return Redirect("~/404");

            article.Views++;
            await _context.SaveChangesAsync();

            List<Comment> comments = await (from c in _context.Comment
                                            where c.PostId == article.Id
                                            && c.Approved == ApprovalStatus.Approved
                                            orderby c.Date descending
                                            select c).ToListAsync();
            ViewData["Comments"] = comments;
            ViewData["PendingComments"] = (await (from c in _context.Comment
                                                  where c.PostId == article.Id
                                                  && c.Approved != ApprovalStatus.Approved
                                                  select c).ToListAsync()).Count;

            return View(article);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment([Bind("Id,Content")] Comment comment, int PostId)
        {
            if (ModelState.IsValid)
            {
                OpenTalonUser user = _userManager.GetUserAsync(User).Result;
                comment.OwnerID = _userManager.GetUserId(User);
                comment.AuthorName = user.Name;
                comment.PostId = PostId;
                comment.Date = DateTime.Now;

                if (User.IsInRole("Staff"))
                    comment.Approved = ApprovalStatus.Approved;
                else
                    comment.Approved = ApprovalStatus.Submitted;

                if (user.Comments == null)
                    user.Comments = new List<Comment>();
                user.Comments.Add(comment);

                _context.Add(comment);
                await _context.SaveChangesAsync();
                return Redirect($"~/article/{PostId}");
            }
            return View(comment);
        }

        [HttpPost("/Home/Search")]
        public IActionResult Search(string filter)
        {
            return Redirect($"/search/{filter}");
        }

        [HttpGet("/search/{filter}/{*sortBy}")]
        public IActionResult Search(string filter, string sortBy = "trending")
        {
            if (string.IsNullOrEmpty(filter)) filter = "all";
            ViewData["Filter"] = filter;

            List<Article> articles;
            switch (filter.ToLower())
            {
                case "all":
                    ViewData["Message"] = "Search All";
                    articles = _context.Article.ToList();
                    break;
                default:
                    ViewData["Message"] = "Search - " + filter;
                    articles = (from a in _context.Article
                                where a.Approved == ApprovalStatus.Approved &&
                                      (a.Title.ToLower().Contains(filter.ToLower()) || 
                                       a.SummaryShort.ToLower().Contains(filter.ToLower()) || 
                                       a.SummaryLong.ToLower().Contains(filter.ToLower()))
                                select a).ToList();
                    break;
            }

            switch (sortBy)
            {
                case "author":
                    ViewData["SortBy"] = "Author";
                    articles = articles.OrderBy(a => a.AuthorName).Take(8).ToList();
                    break;
                case "new":
                    ViewData["SortBy"] = "New";
                    articles = articles.OrderByDescending(a => a.Date).Take(8).ToList();
                    break;
                case "top":
                    ViewData["SortBy"] = "Top";
                    articles = articles.OrderBy(a => a.Views).Take(8).ToList();
                    break;
                default:
                    ViewData["SortBy"] = "Trending";
                    articles = articles.OrderBy(a => a.Views).Take(8).ToList();
                    break;
            }

            ViewData["Query"] = filter;
            return View(articles);
        }

        [HttpGet("/apply")]
        public IActionResult Apply()
        {
            return View();
        }

        [HttpPost("/apply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply([Bind("Id,AuthorName,ThumbnailURL,Title,SummaryShort,SummaryLong,Content")] Article article)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(article.SummaryLong))
                    article.SummaryLong = article.Content.Substring(0, 60) + "...";
                else if (article.SummaryLong.Length > 60)
                    article.SummaryLong = article.SummaryLong.Substring(0, 60) + "...";
                if (string.IsNullOrWhiteSpace(article.ThumbnailURL))
                    article.ThumbnailURL = "/img/img05.jpg";

                OpenTalonUser user = _userManager.GetUserAsync(User).Result;
                article.OwnerID = user.Id;
                article.AuthorName = user.Name + ", " + article.AuthorName;
                article.Date = DateTime.Now;
                article.Views = 0;

                if (User.IsInRole("Staff"))
                {
                    article.Approved = ApprovalStatus.Approved;
                    article.SummaryShort += " Editorial";
                }
                else
                    article.Approved = ApprovalStatus.Submitted;

                if (user.Articles == null)
                    user.Articles = new List<Article>();
                user.Articles.Add(article);

                _context.Add(article);
                await _context.SaveChangesAsync();
                return Redirect("~/success");
            }

            return View(article);
        }

        [HttpGet("/feedback")]
        public IActionResult Feedback()
        {
            return View();
        }

        [HttpPost("/feedback")]
        public async Task<IActionResult> Feedback([Bind("Id,Title,Tags,Content")] Ticket ticket)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(ticket.Title) && string.IsNullOrEmpty(ticket.Content))
                    return View(ticket);

                ticket.OwnerID = _userManager.GetUserId(User);
                ticket.AuthorName = _userManager.GetUserAsync(User).Result.Name;
                ticket.Approved = HandledStatus.Submitted;
                ticket.Date = DateTime.Now;
                ticket.AssignedTo = "None";

                _context.Add(ticket);
                await _context.SaveChangesAsync();
                return Redirect("~/success");
            }

            return View(ticket);
        }

        [Route("/privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        [Route("/404")]
        public IActionResult ItemNotFound()
        {
            return View();
        }

        [Route("/success")]
        public IActionResult ItemSubmitSuccess()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
