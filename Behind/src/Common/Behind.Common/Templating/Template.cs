namespace Behind.Common.Templating;

public abstract record Template(string Name, string Content);

public sealed record SimpleTemplate(string Name, string Content) : Template(Name, Content)
{
    public readonly IEnumerable<Parameter> Parameters = Enumerable.Empty<Parameter>();

    private SimpleTemplate(string Name, string Content, IEnumerable<Parameter> Parameters) : this(Name, Content)
    {
        this.Parameters = Parameters;
    }

    public SimpleTemplate AddParameter(Parameter parameter)
    {
        return new SimpleTemplate(Name, Content, Parameters.Append(parameter));
    }

    public string Render()
    {
        var renderedContent = Content;
        foreach (var parameter in Parameters)
        {
            renderedContent = renderedContent.Replace($"{{{{{parameter.Property}}}}}", parameter.Value);
        }
        return renderedContent;
    }
}