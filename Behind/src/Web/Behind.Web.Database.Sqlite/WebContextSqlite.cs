using Behind.Common.Modules;
using Behind.Contact.Database;
using Behind.Contact.Domain;
using Behind.Core.Database;
using Behind.Forum.Database;
using Behind.Forum.Domain;
using Behind.Quotes.Database;
using Behind.Quotes.Domain;
using Behind.Sport.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Web.Database.Sqlite;

public sealed class WebContextSqlite(DbContextOptions<WebContextSqlite> options, ModuleCatalog modules)
    : AbstractCoreContext(options, modules), IQuotesContext, ISportContext, IForumContext, IContactContext
{
    public DbSet<ForumBoardCategory> ForumBoardCategories => Set<ForumBoardCategory>();

    public DbSet<ForumBoard> ForumBoards => Set<ForumBoard>();

    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();

    public DbSet<ForumUserModeration> ForumUserModerations => Set<ForumUserModeration>();

    public DbSet<ContactForm> ContactForms => Set<ContactForm>();

    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<Discipline> Disciplines => Set<Discipline>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<StatType> StatTypes => Set<StatType>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerSeason> PlayerSeasons => Set<PlayerSeason>();

    public DbSet<Stat> Stats => Set<Stat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (!Modules.Modules.Any(module => module.Key == "contact"))
        {
            modelBuilder.Ignore<ContactForm>();
            modelBuilder.Ignore<ContactRequest>();
        }
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyCoreProviderConventions();
    }
}
