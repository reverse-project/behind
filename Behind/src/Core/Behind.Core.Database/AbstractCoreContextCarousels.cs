using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public partial class AbstractCoreContext
{
    public DbSet<Carousel> Carousels => Set<Carousel>();

    public DbSet<CarouselSlide> CarouselSlides => Set<CarouselSlide>();
}
