﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Contra.Areas.Identity.Data;

namespace Contra.Data
{
    public class ApplicationDbContext : IdentityDbContext<OpenTalonUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Models.Comment> Comment { get; set; }

        public DbSet<Models.Article> Article { get; set; }

        public DbSet<Models.Ticket> Ticket { get; set; }
    }
}