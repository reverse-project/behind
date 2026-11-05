using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class CarouselDraftExtensions
{
    extension(CarouselEndpoints.SaveCarouselRequest request)
    {
        public Option<ICarouselCommands.CarouselDraft> ToDraft(CarouselsConfiguration configuration)
        {
            var slides = (request.Slides ?? []).Select(slide => slide.ToDraft(configuration)).ToList();

            return !configuration.IsValidInterval(request.IntervalSeconds)
                   || slides.Count == 0
                   || slides.Count > configuration.SlideLimit
                   || slides.Any(slide => slide.IsNone())
                ? None
                : Some(new ICarouselCommands.CarouselDraft(
                    request.IntervalSeconds,
                    slides.SelectMany(slide => slide.AsEnumerable()).ToList()));
        }
    }

    extension(CarouselEndpoints.SaveCarouselSlideRequest slide)
    {
        private Option<ICarouselCommands.CarouselSlideDraft> ToDraft(CarouselsConfiguration configuration)
        {
            var caption = (slide.Caption ?? string.Empty).Trim();
            var link = (slide.LinkUrl ?? string.Empty).Trim();

            return slide.ImageId == Guid.Empty
                   || caption.Length > configuration.CaptionLimit
                   || !link.IsSafeCarouselLink
                ? None
                : Some(new ICarouselCommands.CarouselSlideDraft(slide.ImageId, caption, link));
        }
    }
}
