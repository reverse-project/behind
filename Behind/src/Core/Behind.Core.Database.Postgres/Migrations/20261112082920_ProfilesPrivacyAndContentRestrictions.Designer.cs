using System;
using System.Collections.Generic;
using Behind.Core.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations;

[DbContext(typeof(CoreContextPostgres))]
[Migration("20261112082920_ProfilesPrivacyAndContentRestrictions")]
partial class _20261112082920_ProfilesPrivacyAndContentRestrictions
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("Behind:AgeRestrictionParents", new[] { "NewsId", "ArticleId", "GalleryId", "CarouselId", "FolderId", "BoardId", "TopicId", "CategoryId", "TeamId", "PlayerId", "SeasonId", "DisciplineId", "PlayerSeasonId", "StatTypeId", "SiteId", "ParentId", "PositionId", "SubjectId" })
            .HasAnnotation("ProductVersion", "11.0.0-rc.3.26522.148")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Behind.Common.Database.ActiveVersion", b =>
            {
                b.Property<string>("EntityType")
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("EntityKey")
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.HasKey("EntityType", "EntityKey");

                b.ToTable("ActiveVersions", t =>
                    {
                        t.ExcludeFromMigrations();
                    });
            });

        modelBuilder.Entity("Behind.Core.Domain.AccessSettings", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<bool>("ModeratedSignUp")
                    .HasColumnType("boolean");

                b.Property<string>("RestrictedToRole")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<bool>("RestrictionEnabled")
                    .HasColumnType("boolean");

                b.Property<string>("RestrictionMessage")
                    .IsRequired()
                    .HasMaxLength(20000)
                    .HasColumnType("character varying(20000)");

                b.Property<string>("RestrictionTitle")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)")
                    .HasDefaultValue("");

                b.HasKey("Key", "Version");

                b.ToTable("AccessSettings");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Article", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("AuthorId")
                    .HasColumnType("uuid");

                b.Property<Guid?>("CategoryId")
                    .HasColumnType("uuid");

                b.Property<string>("Content")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<string>("Summary")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("Key", "Version");

                b.HasIndex("AuthorId");

                b.HasIndex("CategoryId");

                b.ToTable("Articles");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.ArticleCategory", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid?>("HeaderImageId")
                    .HasColumnType("uuid");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.HasKey("Key", "Version");

                b.HasIndex("Name");

                b.ToTable("ArticleCategories");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.AuditLogEntry", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Action")
                    .IsRequired()
                    .HasMaxLength(4000)
                    .HasColumnType("character varying(4000)");

                b.Property<Guid?>("BehalfOfId")
                    .HasColumnType("uuid");

                b.Property<string>("EntityKey")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)");

                b.Property<string>("EntityName")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<long>("Timestamp")
                    .HasColumnType("bigint");

                b.Property<Guid?>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("BehalfOfId");

                b.HasIndex("Timestamp");

                b.HasIndex("UserId");

                b.HasIndex("EntityName", "EntityKey");

                b.ToTable("AuditLogEntries");
            });

        modelBuilder.Entity("Behind.Core.Domain.Carousel", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<int>("IntervalSeconds")
                    .HasColumnType("integer");

                b.HasKey("Key", "Version");

                b.ToTable("Carousels");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.CarouselSlide", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Caption")
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)");

                b.Property<Guid>("CarouselId")
                    .HasColumnType("uuid");

                b.Property<Guid>("ImageId")
                    .HasColumnType("uuid");

                b.Property<string>("LinkUrl")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.Property<int>("Position")
                    .HasColumnType("integer");

                b.HasKey("Key", "Version");

                b.HasIndex("ImageId");

                b.HasIndex("CarouselId", "Version");

                b.ToTable("CarouselSlides");

                b.HasAnnotation("Behind:AgeRestricted", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.ChatMessage", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("AuthorId")
                    .HasColumnType("uuid");

                b.Property<bool>("IsAnonymous")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                b.Property<Guid?>("ReplyToId")
                    .HasColumnType("uuid");

                b.Property<string>("Text")
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)");

                b.HasKey("Key", "Version");

                b.HasIndex("AuthorId");

                b.HasIndex("ReplyToId");

                b.ToTable("ChatMessages");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.ContentRestriction", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("EntityId")
                    .HasColumnType("uuid");

                b.Property<string>("EntityType")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<int>("MinimumAge")
                    .HasColumnType("integer");

                b.HasKey("Key", "Version");

                b.HasIndex("EntityType", "EntityId");

                b.ToTable("ContentRestrictions");
            });

        modelBuilder.Entity("Behind.Core.Domain.Download", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<Guid>("FileId")
                    .HasColumnType("uuid");

                b.Property<Guid>("FolderId")
                    .HasColumnType("uuid");

                b.Property<Guid>("UploadedById")
                    .HasColumnType("uuid");

                b.HasKey("Key", "Version");

                b.HasIndex("FileId");

                b.HasIndex("FolderId");

                b.HasIndex("UploadedById");

                b.ToTable("Downloads");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.DownloadFolder", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<Guid?>("ParentId")
                    .HasColumnType("uuid");

                b.Property<string>("Slug")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.HasKey("Key", "Version");

                b.HasIndex("ParentId");

                b.ToTable("DownloadFolders");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Email", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<long?>("ConfirmedAt")
                    .HasColumnType("bigint");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)");

                b.HasKey("Id");

                b.HasIndex("UserId");

                b.ToTable("Emails");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Gallery", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("AuthorId")
                    .HasColumnType("uuid");

                b.Property<string>("Description")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<bool>("ThumbnailGenerated")
                    .HasColumnType("boolean");

                b.Property<Guid?>("ThumbnailImageId")
                    .HasColumnType("uuid");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("Key", "Version");

                b.HasIndex("AuthorId");

                b.HasIndex("Title");

                b.ToTable("Galleries");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.GalleryItem", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Description")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.Property<Guid>("GalleryId")
                    .HasColumnType("uuid");

                b.Property<Guid>("ImageId")
                    .HasColumnType("uuid");

                b.Property<int>("Position")
                    .HasColumnType("integer");

                b.Property<Guid>("ThumbnailId")
                    .HasColumnType("uuid");

                b.HasKey("Key", "Version");

                b.HasIndex("ImageId");

                b.HasIndex("ThumbnailId");

                b.HasIndex("GalleryId", "Version");

                b.ToTable("GalleryItems");

                b.HasAnnotation("Behind:AgeRestricted", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Grant", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)");

                b.HasKey("Id");

                b.HasAlternateKey("Name");

                b.ToTable("Grants");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.MagicLink", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<long?>("ConsumedAtValue")
                    .HasColumnType("bigint")
                    .HasColumnName("ConsumedAt");

                b.Property<long>("ExpiresAt")
                    .HasColumnType("bigint");

                b.Property<string>("Function")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)")
                    .HasDefaultValue("SignIn");

                b.Property<long>("IssuedAt")
                    .HasColumnType("bigint");

                b.Property<Guid>("Nonce")
                    .HasColumnType("uuid");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("Nonce")
                    .IsUnique();

                b.HasIndex("UserId");

                b.ToTable("MagicLinks");
            });

        modelBuilder.Entity("Behind.Core.Domain.Menu", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("Key", "Version");

                b.HasIndex("Name");

                b.ToTable("Menus");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.MenuItem", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<Guid>("MenuId")
                    .HasColumnType("uuid");

                b.Property<Guid?>("ParentId")
                    .HasColumnType("uuid");

                b.Property<int>("Position")
                    .HasColumnType("integer");

                b.Property<string>("Target")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<string>("Url")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.HasKey("Key", "Version");

                b.HasIndex("MenuId");

                b.HasIndex("ParentId");

                b.ToTable("MenuItems");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.News", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("AuthorId")
                    .HasColumnType("uuid");

                b.Property<Guid?>("CategoryId")
                    .HasColumnType("uuid");

                b.Property<string>("Content")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<long?>("EndAtUtc")
                    .HasColumnType("bigint");

                b.Property<bool>("Highlighted")
                    .HasColumnType("boolean");

                b.Property<long?>("StartAtUtc")
                    .HasColumnType("bigint");

                b.Property<string>("Summary")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("Key", "Version");

                b.HasIndex("AuthorId");

                b.HasIndex("CategoryId");

                b.HasIndex("StartAtUtc");

                b.ToTable("News");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.NewsCategory", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid?>("HeaderImageId")
                    .HasColumnType("uuid");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.HasKey("Key", "Version");

                b.HasIndex("Name");

                b.ToTable("NewsCategories");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.NewsComment", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<Guid>("AuthorId")
                    .HasColumnType("uuid");

                b.Property<string>("Content")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.Property<bool>("IsAnonymous")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                b.Property<Guid>("NewsId")
                    .HasColumnType("uuid");

                b.HasKey("Key", "Version");

                b.HasIndex("AuthorId");

                b.HasIndex("NewsId");

                b.ToTable("NewsComments");

                b
                    .HasAnnotation("Behind:AgeRestricted", true)
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Password", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<bool>("IsActive")
                    .HasColumnType("boolean");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)");

                b.HasKey("Id");

                b.HasIndex("UserId");

                b.ToTable("Passwords");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Restriction", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)");

                b.HasKey("Id");

                b.HasAlternateKey("Name");

                b.ToTable("Restrictions");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Role", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("text");

                b.HasKey("Id");

                b.ToTable((string)null);

                b
                    .UseTpcMappingStrategy()
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.RouteAlias", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Alias")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("Target")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.HasKey("Key", "Version");

                b.HasIndex("Alias");

                b.ToTable("RouteAliases");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.Site", b =>
            {
                b.Property<Guid>("SiteId")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Content")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<long>("CreatedAt")
                    .HasColumnType("bigint");

                b.Property<bool>("HideTitle")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                b.Property<long?>("PublishedAtUtc")
                    .HasColumnType("bigint");

                b.Property<string>("Slug")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("SiteId", "Version");

                b.HasIndex("Slug");

                b.ToTable("Sites");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.SiteSection", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("Content")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<string>("ImageAlt")
                    .IsRequired()
                    .HasMaxLength(300)
                    .HasColumnType("character varying(300)");

                b.Property<Guid?>("ImageId")
                    .HasColumnType("uuid");

                b.Property<string>("ImagePosition")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<int>("Position")
                    .HasColumnType("integer");

                b.Property<Guid>("SiteId")
                    .HasColumnType("uuid");

                b.Property<string>("Theme")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("character varying(20)");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.HasKey("Key", "Version");

                b.HasIndex("ImageId");

                b.HasIndex("SiteId", "Version");

                b.ToTable("SiteSections");

                b.HasAnnotation("Behind:AgeRestricted", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.User", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.ToTable((string)null);

                b
                    .UseTpcMappingStrategy()
                    .HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.UserAgent", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)");

                b.HasKey("Id");

                b.HasIndex("Value")
                    .IsUnique();

                b.ToTable("UserAgents");
            });

        modelBuilder.Entity("Behind.Core.Domain.UserPasskey", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("CredentialId")
                    .IsRequired()
                    .HasMaxLength(1400)
                    .HasColumnType("character varying(1400)");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("PublicKey")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("character varying(2000)");

                b.Property<long>("SignCount")
                    .HasColumnType("bigint");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("CredentialId")
                    .IsUnique();

                b.HasIndex("UserId");

                b.ToTable("UserPasskeys");
            });

        modelBuilder.Entity("Behind.Core.Domain.UserRecoveryCode", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<string>("Hash")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("UserId");

                b.ToTable("UserRecoveryCodes");
            });

        modelBuilder.Entity("Behind.Core.Domain.UserTotp", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<bool>("IsConfirmed")
                    .HasColumnType("boolean");

                b.Property<long>("LastUsedStep")
                    .HasColumnType("bigint");

                b.Property<string>("Secret")
                    .IsRequired()
                    .HasMaxLength(128)
                    .HasColumnType("character varying(128)");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("UserId")
                    .IsUnique();

                b.ToTable("UserTotps");
            });

        modelBuilder.Entity("Behind.Core.Domain.ValidSession", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<Guid?>("ImpersonatorId")
                    .HasColumnType("uuid");

                b.Property<long?>("InvalidatedAt")
                    .HasColumnType("bigint");

                b.Property<Guid?>("InvalidatedById")
                    .HasColumnType("uuid");

                b.Property<string>("RemoteAddress")
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnType("character varying(255)");

                b.Property<Guid>("UserAgentId")
                    .HasColumnType("uuid");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.ComplexProperty(typeof(Dictionary<string, object>), "AccessToken", "Behind.Core.Domain.ValidSession.AccessToken#AccessToken", b1 =>
                    {
                        b1.IsRequired();

                        b1.Property<long>("ExpiresAt")
                            .HasColumnType("bigint");

                        b1.Property<long>("IssuedAt")
                            .HasColumnType("bigint");

                        b1.Property<Guid>("Nonce")
                            .HasColumnType("uuid");
                    });

                b.ComplexProperty(typeof(Dictionary<string, object>), "RefreshToken", "Behind.Core.Domain.ValidSession.RefreshToken#RefreshToken", b1 =>
                    {
                        b1.IsRequired();

                        b1.Property<long>("ExpiresAt")
                            .HasColumnType("bigint");

                        b1.Property<long>("IssuedAt")
                            .HasColumnType("bigint");

                        b1.Property<Guid>("Nonce")
                            .HasColumnType("uuid");
                    });

                b.HasKey("Id");

                b.HasIndex("ImpersonatorId");

                b.HasIndex("InvalidatedAt");

                b.HasIndex("InvalidatedById");

                b.HasIndex("UserAgentId");

                b.HasIndex("UserId");

                b.ToTable("Sessions");
            });

        modelBuilder.Entity("Behind.Core.Domain.VfsFile", b =>
            {
                b.Property<Guid>("Key")
                    .HasColumnType("uuid");

                b.Property<long>("Version")
                    .HasColumnType("bigint");

                b.Property<string>("ContentType")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.Property<string>("FileName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("character varying(200)");

                b.Property<long>("Length")
                    .HasColumnType("bigint");

                b.Property<Guid?>("ParentId")
                    .HasColumnType("uuid");

                b.Property<string>("Type")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("character varying(100)");

                b.HasKey("Key", "Version");

                b.HasIndex("ParentId");

                b.HasIndex("Type");

                b.ToTable("VfsFiles");

                b.HasAnnotation("Behind:AgeRestricted", true);
            });

        modelBuilder.Entity("Behind.Core.Domain.WallOfShameEntry", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<long?>("ExpiresAt")
                    .HasColumnType("bigint");

                b.Property<Guid>("GivenById")
                    .HasColumnType("uuid");

                b.Property<string>("Reason")
                    .IsRequired()
                    .HasColumnType("text");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("Id");

                b.HasIndex("ExpiresAt");

                b.HasIndex("GivenById");

                b.HasIndex("UserId");

                b.ToTable("WallOfShameEntries");

                b.HasAnnotation("Behind:SoftDeletable", true);
            });

        modelBuilder.Entity("MenuItemRoles", b =>
            {
                b.Property<Guid>("MenuItemKey")
                    .HasColumnType("uuid");

                b.Property<long>("MenuItemVersion")
                    .HasColumnType("bigint");

                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid");

                b.HasKey("MenuItemKey", "MenuItemVersion", "RoleId");

                b.HasIndex("RoleId");

                b.ToTable("MenuItemRoles");
            });

        modelBuilder.Entity("PendingUserApprovals", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("UserId");

                b.ToTable("PendingUserApprovals");
            });

        modelBuilder.Entity("RoleGrants", b =>
            {
                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid");

                b.Property<Guid>("GrantId")
                    .HasColumnType("uuid");

                b.HasKey("RoleId", "GrantId");

                b.HasIndex("GrantId");

                b.ToTable("RoleGrants");
            });

        modelBuilder.Entity("RoleRestrictions", b =>
            {
                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid");

                b.Property<Guid>("RestrictionId")
                    .HasColumnType("uuid");

                b.HasKey("RoleId", "RestrictionId");

                b.HasIndex("RestrictionId");

                b.ToTable("RoleRestrictions");
            });

        modelBuilder.Entity("UserGrants", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.Property<Guid>("GrantId")
                    .HasColumnType("uuid");

                b.HasKey("UserId", "GrantId");

                b.HasIndex("GrantId");

                b.ToTable("UserGrants");
            });

        modelBuilder.Entity("UserRestrictions", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.Property<Guid>("RestrictionId")
                    .HasColumnType("uuid");

                b.HasKey("UserId", "RestrictionId");

                b.HasIndex("RestrictionId");

                b.ToTable("UserRestrictions");
            });

        modelBuilder.Entity("UserRoles", b =>
            {
                b.Property<Guid>("RoleId")
                    .HasColumnType("uuid");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.HasKey("RoleId", "UserId");

                b.HasIndex("UserId");

                b.ToTable("UserRoles");
            });

        modelBuilder.Entity("UserSettings", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid");

                b.Property<string>("Key")
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)");

                b.Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)");

                b.HasKey("UserId", "Key");

                b.ToTable("UserSettings");
            });

        modelBuilder.Entity("Behind.Core.Domain.UserRole", b =>
            {
                b.HasBaseType("Behind.Core.Domain.Role");

                b.Property<bool>("IsDeveloper")
                    .HasColumnType("boolean");

                b.HasIndex("Name");

                b.ToTable("Roles");
            });

        modelBuilder.Entity("Behind.Core.Domain.HumanUser", b =>
            {
                b.HasBaseType("Behind.Core.Domain.User");

                b.Property<DateOnly?>("Birthday")
                    .HasColumnType("date");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)");

                b.Property<bool>("HideEmail")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                b.Property<Guid?>("ProfilePictureId")
                    .HasColumnType("uuid");

                b.HasIndex("DisplayName");

                b.ToTable("Users");
            });

        modelBuilder.Entity("Behind.Core.Domain.Article", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("AuthorId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.AuditLogEntry", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("BehalfOfId")
                    .OnDelete(DeleteBehavior.NoAction);

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction);
            });

        modelBuilder.Entity("Behind.Core.Domain.Download", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UploadedById")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.Email", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany("Emails")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.Gallery", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("AuthorId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.MagicLink", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.News", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("AuthorId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.NewsComment", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("AuthorId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.Password", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.UserPasskey", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.UserRecoveryCode", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.UserTotp", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.ValidSession", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("ImpersonatorId")
                    .OnDelete(DeleteBehavior.NoAction);

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("InvalidatedById")
                    .OnDelete(DeleteBehavior.NoAction);

                b.HasOne("Behind.Core.Domain.UserAgent", null)
                    .WithMany()
                    .HasForeignKey("UserAgentId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.WallOfShameEntry", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("GivenById")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("MenuItemRoles", b =>
            {
                b.HasOne("Behind.Core.Domain.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.MenuItem", null)
                    .WithMany()
                    .HasForeignKey("MenuItemKey", "MenuItemVersion")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("PendingUserApprovals", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("RoleGrants", b =>
            {
                b.HasOne("Behind.Core.Domain.Grant", null)
                    .WithMany()
                    .HasForeignKey("GrantId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("RoleRestrictions", b =>
            {
                b.HasOne("Behind.Core.Domain.Restriction", null)
                    .WithMany()
                    .HasForeignKey("RestrictionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("UserGrants", b =>
            {
                b.HasOne("Behind.Core.Domain.Grant", null)
                    .WithMany()
                    .HasForeignKey("GrantId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("UserRestrictions", b =>
            {
                b.HasOne("Behind.Core.Domain.Restriction", null)
                    .WithMany()
                    .HasForeignKey("RestrictionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("UserRoles", b =>
            {
                b.HasOne("Behind.Core.Domain.Role", null)
                    .WithMany()
                    .HasForeignKey("RoleId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("UserSettings", b =>
            {
                b.HasOne("Behind.Core.Domain.HumanUser", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
            });

        modelBuilder.Entity("Behind.Core.Domain.HumanUser", b =>
            {
                b.Navigation("Emails");
            });
#pragma warning restore 612, 618
    }
}
