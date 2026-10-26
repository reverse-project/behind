using System.Collections;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Templating;

public sealed class Templates : IEnumerable<Template>
{
    private readonly List<Template> _templates = [];

    public void Add(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        _templates.Add(template);
    }

    public void Add(string name, string content)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);

        _templates.Add(new SimpleTemplate(name, content));
    }

    public void Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        _templates.RemoveAll(t => t.Name == name);
    }

    public void Remove(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        _templates.Remove(template);
    }

    public IEnumerator<Template> GetEnumerator() => _templates.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public Option<Template> Get(string name)
    {
        var template = _templates.SingleOrDefault(t => t.Name == name);
        return template is null ? None : template;
    }

    public Template GetOrDefault(string name, Template defaultTemplate)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(defaultTemplate);

        return _templates.SingleOrDefault(t => t.Name == name) ?? defaultTemplate;
    }
}