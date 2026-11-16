namespace Behind.Common;

public static class ContentAgePolicy
{
    public const int MaximumAge = 150;

    public static int Age(DateOnly? birthday, DateOnly today)
    {
        if (birthday is null || birthday > today)
        {
            return -1;
        }

        var years = today.Year - birthday.Value.Year;
        return birthday.Value.AddYears(years) > today ? years - 1 : years;
    }

    public static bool Allows(int minimumAge, DateOnly? birthday, DateOnly today) =>
        minimumAge == 0 || Age(birthday, today) >= minimumAge;
}
