define([], function () {
    var escape = function (text) {
        return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    };
    var safeUrl = function (url) {
        return /^(https?:\/\/|mailto:)[^\s<>"'\[\]]+$/i.test(url);
    };
    var tags = { b: "strong", i: "em", u: "u", s: "s", quote: "blockquote", code: "pre", list: "ul", "*": "li", url: "a" };

    var render = function (source) {
        source = source || "";
        var root = { children: [] };
        var stack = [root];
        var tokens = /\[(\/?)(b|i|u|s|quote|code|list|\*|url|lb|rb)(?:=([^\]\r\n]*))?\]/gi;
        var position = 0;
        var match;
        var append = function (text) {
            stack[stack.length - 1].children.push(escape(text).replace(/\r\n|\r|\n/g, "<br>"));
        };
        while ((match = tokens.exec(source))) {
            append(source.slice(position, match.index));
            position = tokens.lastIndex;
            var name = match[2].toLowerCase();
            var parent = stack[stack.length - 1];
            if (!match[1] && (name === "lb" || name === "rb")) {
                append(name === "lb" ? "[" : "]");
            } else if (match[1]) {
                if (parent.name === name) {
                    parent.closed = true;
                    stack.pop();
                } else if (name === "list" && parent.name === "*" && stack[stack.length - 2].name === "list") {
                    parent.closed = true;
                    stack.pop();
                    stack.pop().closed = true;
                } else {
                    append(match[0]);
                }
            } else if (name === "code") {
                var end = source.toLowerCase().indexOf("[/code]", position);
                if (end < 0) {
                    append(match[0]);
                } else {
                    parent.children.push("<pre><code>" + escape(source.slice(position, end)
                        .replace(/\[(lb|rb)\]/gi, function (token, bracket) { return bracket.toLowerCase() === "lb" ? "[" : "]"; })) + "</code></pre>");
                    tokens.lastIndex = position = end + 7;
                }
            } else if (stack.length < 32 && tags[name]) {
                if (name === "*" && parent.name === "*") {
                    parent.closed = true;
                    stack.pop();
                    parent = stack[stack.length - 1];
                }
                if (name === "*" && parent.name !== "list") {
                    append(match[0]);
                } else {
                    var node = { name: name, argument: match[3], opening: match[0], children: [], closed: false };
                    parent.children.push(node);
                    stack.push(node);
                }
            } else {
                append(match[0]);
            }
        }
        append(source.slice(position));
        var html = function (node) {
            if (typeof node === "string") {
                return node;
            }
            var content = node.children.map(html).join("");
            if (!node.name) {
                return content;
            }
            if (!node.closed) {
                return escape(node.opening) + content;
            }
            if (node.name === "url") {
                var url = node.argument === undefined ? content.replace(/&amp;/g, "&") : node.argument;
                return safeUrl(url) ? '<a href="' + escape(url) + '" target="_blank" rel="nofollow noopener noreferrer">' + content + "</a>" : content;
            }
            var tag = node.name === "list" && node.argument === "1" ? "ol" : tags[node.name];
            return "<" + tag + ">" + content + "</" + tag + ">";
        };
        return html(root);
    };

    var fromHtml = function (html) {
        var container = document.createElement("div");
        container.innerHTML = html;
        var literal = function (text) {
            return text.replace(/[\[\]]/g, function (bracket) { return bracket === "[" ? "[lb]" : "[rb]"; });
        };
        var read = function (node) {
            if (node.nodeType === 3) {
                return literal(node.nodeValue.replace(/\u00a0/g, " "));
            }
            var name = node.nodeName.toLowerCase();
            if (name === "script" || name === "style") {
                return "";
            }
            if (name === "pre") {
                var code = node.hasAttribute("data-language") ? node.textContent.replace(/\n$/, "") : node.textContent;
                return "[code]" + literal(code) + "[/code]\n";
            }
            var content = Array.prototype.map.call(node.childNodes, read).join("");
            var format = { strong: "b", b: "b", em: "i", i: "i", u: "u", s: "s", strike: "s", blockquote: "quote" }[name];
            if (format) {
                return "[" + format + "]" + content.replace(/\n$/, "") + "[/" + format + "]" + (name === "blockquote" ? "\n" : "");
            }
            if (name === "a") {
                var url = node.getAttribute("href") || "";
                return safeUrl(url) ? "[url=" + url + "]" + content + "[/url]" : content;
            }
            if (name === "ul" || name === "ol") {
                return (name === "ol" ? "[list=1]" : "[list]") + content + "[/list]\n";
            }
            if (name === "li") {
                return "[*]" + content.replace(/\n$/, "") + "[/*]";
            }
            if (name === "br") {
                return "\n";
            }
            return content + (name === "p" || name === "div" ? "\n" : "");
        };
        return Array.prototype.map.call(container.childNodes, read).join("").replace(/\n$/, "");
    };

    return { render: render, fromHtml: fromHtml };
});
