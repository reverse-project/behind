using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleCategories",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HeaderImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleCategories", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Carousels",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IntervalSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carousels", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "CarouselSlides",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CarouselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarouselSlides", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "DownloadFolders",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadFolders", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "GalleryItems",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GalleryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryItems", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Id);
                    table.UniqueConstraint("AK_Grants_Name", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Target = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "NewsCategories",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HeaderImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsCategories", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Restrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restrictions", x => x.Id);
                    table.UniqueConstraint("AK_Restrictions_Name", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsDeveloper = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RouteAliases",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Target = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteAliases", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    HideTitle = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PublishedAtUtc = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => new { x.SiteId, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SiteSections",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Theme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ImagePosition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSections", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "UserAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VfsFiles",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VfsFiles", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "RoleGrants",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGrants", x => new { x.RoleId, x.GrantId });
                    table.ForeignKey(
                        name: "FK_RoleGrants_Grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "Grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemRoles",
                columns: table => new
                {
                    MenuItemKey = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemVersion = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemRoles", x => new { x.MenuItemKey, x.MenuItemVersion, x.RoleId });
                    table.ForeignKey(
                        name: "FK_MenuItemRoles_MenuItems_MenuItemKey_MenuItemVersion",
                        columns: x => new { x.MenuItemKey, x.MenuItemVersion },
                        principalTable: "MenuItems",
                        principalColumns: new[] { "Key", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleRestrictions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleRestrictions", x => new { x.RoleId, x.RestrictionId });
                    table.ForeignKey(
                        name: "FK_RoleRestrictions_Restrictions_RestrictionId",
                        column: x => x.RestrictionId,
                        principalTable: "Restrictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_Articles_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    BehalfOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_Users_BehalfOfId",
                        column: x => x.BehalfOfId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLogEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Downloads",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Downloads", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_Downloads_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ConfirmedAt = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Emails_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Galleries",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThumbnailGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    ThumbnailImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galleries", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_Galleries_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MagicLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    Function = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SignIn"),
                    ConsumedAt = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagicLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Highlighted = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndAtUtc = table.Column<long>(type: "bigint", nullable: true),
                    StartAtUtc = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_News_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsComments",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsComments", x => new { x.Key, x.Version });
                    table.ForeignKey(
                        name: "FK_NewsComments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Passwords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passwords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passwords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteAddress = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImpersonatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvalidatedAt = table.Column<long>(type: "bigint", nullable: true),
                    InvalidatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UserAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessToken_ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    AccessToken_IssuedAt = table.Column<long>(type: "bigint", nullable: false),
                    AccessToken_Nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshToken_ExpiresAt = table.Column<long>(type: "bigint", nullable: false),
                    RefreshToken_IssuedAt = table.Column<long>(type: "bigint", nullable: false),
                    RefreshToken_Nonce = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_UserAgents_UserAgentId",
                        column: x => x.UserAgentId,
                        principalTable: "UserAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_ImpersonatorId",
                        column: x => x.ImpersonatorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_InvalidatedById",
                        column: x => x.InvalidatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserGrants",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGrants", x => new { x.UserId, x.GrantId });
                    table.ForeignKey(
                        name: "FK_UserGrants_Grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "Grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPasskeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CredentialId = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPasskeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPasskeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRecoveryCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRestrictions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRestrictions", x => new { x.UserId, x.RestrictionId });
                    table.ForeignKey(
                        name: "FK_UserRestrictions_Restrictions_RestrictionId",
                        column: x => x.RestrictionId,
                        principalTable: "Restrictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRestrictions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.RoleId, x.UserId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => new { x.UserId, x.Key });
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTotps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedStep = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTotps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTotps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WallOfShameEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<long>(type: "bigint", nullable: true),
                    GivenById = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WallOfShameEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WallOfShameEntries_Users_GivenById",
                        column: x => x.GivenById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WallOfShameEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategories_Name",
                table: "ArticleCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_AuthorId",
                table: "Articles",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_CategoryId",
                table: "Articles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_BehalfOfId",
                table: "AuditLogEntries",
                column: "BehalfOfId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityName_EntityKey",
                table: "AuditLogEntries",
                columns: new[] { "EntityName", "EntityKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_Timestamp",
                table: "AuditLogEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_UserId",
                table: "AuditLogEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CarouselSlides_CarouselId_Version",
                table: "CarouselSlides",
                columns: new[] { "CarouselId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_CarouselSlides_ImageId",
                table: "CarouselSlides",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadFolders_ParentId",
                table: "DownloadFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_FileId",
                table: "Downloads",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_FolderId",
                table: "Downloads",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_UploadedById",
                table: "Downloads",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_UserId",
                table: "Emails",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_AuthorId",
                table: "Galleries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_Title",
                table: "Galleries",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_GalleryId_Version",
                table: "GalleryItems",
                columns: new[] { "GalleryId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_ImageId",
                table: "GalleryItems",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_GalleryItems_ThumbnailId",
                table: "GalleryItems",
                column: "ThumbnailId");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinks_Nonce",
                table: "MagicLinks",
                column: "Nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinks_UserId",
                table: "MagicLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRoles_RoleId",
                table: "MenuItemRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuId",
                table: "MenuItems",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ParentId",
                table: "MenuItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_Name",
                table: "Menus",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_News_AuthorId",
                table: "News",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_News_CategoryId",
                table: "News",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_News_StartAtUtc",
                table: "News",
                column: "StartAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NewsCategories_Name",
                table: "NewsCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NewsComments_AuthorId",
                table: "NewsComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsComments_NewsId",
                table: "NewsComments",
                column: "NewsId");

            migrationBuilder.CreateIndex(
                name: "IX_Passwords_UserId",
                table: "Passwords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleGrants_GrantId",
                table: "RoleGrants",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleRestrictions_RestrictionId",
                table: "RoleRestrictions",
                column: "RestrictionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RouteAliases_Alias",
                table: "RouteAliases",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ImpersonatorId",
                table: "Sessions",
                column: "ImpersonatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InvalidatedAt",
                table: "Sessions",
                column: "InvalidatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_InvalidatedById",
                table: "Sessions",
                column: "InvalidatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserAgentId",
                table: "Sessions",
                column: "UserAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Slug",
                table: "Sites",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSections_ImageId",
                table: "SiteSections",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteSections_SiteId_Version",
                table: "SiteSections",
                columns: new[] { "SiteId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAgents_Value",
                table: "UserAgents",
                column: "Value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGrants_GrantId",
                table: "UserGrants",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_CredentialId",
                table: "UserPasskeys",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeys_UserId",
                table: "UserPasskeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodes_UserId",
                table: "UserRecoveryCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRestrictions_RestrictionId",
                table: "UserRestrictions",
                column: "RestrictionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                table: "Users",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_UserTotps_UserId",
                table: "UserTotps",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VfsFiles_ParentId",
                table: "VfsFiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VfsFiles_Type",
                table: "VfsFiles",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_ExpiresAt",
                table: "WallOfShameEntries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_GivenById",
                table: "WallOfShameEntries",
                column: "GivenById");

            migrationBuilder.CreateIndex(
                name: "IX_WallOfShameEntries_UserId",
                table: "WallOfShameEntries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleCategories");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "Carousels");

            migrationBuilder.DropTable(
                name: "CarouselSlides");

            migrationBuilder.DropTable(
                name: "DownloadFolders");

            migrationBuilder.DropTable(
                name: "Downloads");

            migrationBuilder.DropTable(
                name: "Emails");

            migrationBuilder.DropTable(
                name: "Galleries");

            migrationBuilder.DropTable(
                name: "GalleryItems");

            migrationBuilder.DropTable(
                name: "MagicLinks");

            migrationBuilder.DropTable(
                name: "MenuItemRoles");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "NewsCategories");

            migrationBuilder.DropTable(
                name: "NewsComments");

            migrationBuilder.DropTable(
                name: "Passwords");

            migrationBuilder.DropTable(
                name: "RoleGrants");

            migrationBuilder.DropTable(
                name: "RoleRestrictions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "RouteAliases");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "SiteSections");

            migrationBuilder.DropTable(
                name: "UserGrants");

            migrationBuilder.DropTable(
                name: "UserPasskeys");

            migrationBuilder.DropTable(
                name: "UserRecoveryCodes");

            migrationBuilder.DropTable(
                name: "UserRestrictions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "UserTotps");

            migrationBuilder.DropTable(
                name: "VfsFiles");

            migrationBuilder.DropTable(
                name: "WallOfShameEntries");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "UserAgents");

            migrationBuilder.DropTable(
                name: "Grants");

            migrationBuilder.DropTable(
                name: "Restrictions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
