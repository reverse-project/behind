define(["quill"], function (Quill) {
    var alignments = ["left", "center", "right"];
    var minimumWidth = 32;
    var sizes = [25, 50, 75, 100];

    var alignmentClass = function (alignment) {
        return "ql-image-" + alignment;
    };

    var BaseImage = Quill.import("formats/image");

    class ResizableImage extends BaseImage {
        static formats(domNode) {
            var formats = super.formats(domNode);
            var alignment = alignments.filter(function (name) {
                return domNode.classList.contains(alignmentClass(name));
            })[0];

            return alignment ? Object.assign({}, formats, { imageAlign: alignment }) : formats;
        }

        format(name, value) {
            if (name !== "imageAlign") {
                super.format(name, value);

                return;
            }

            alignments.forEach(function (alignment) {
                this.domNode.classList.remove(alignmentClass(alignment));
            }, this);

            if (alignments.indexOf(value) > -1) {
                this.domNode.classList.add(alignmentClass(value));
            }

            if (this.domNode.classList.length === 0) {
                this.domNode.removeAttribute("class");
            }
        }
    }

    var register = function () {
        Quill.register("formats/image", ResizableImage, true);
    };

    var button = function (label, title, action) {
        var element = document.createElement("button");

        element.type = "button";
        element.className = "btn btn-sm btn-light border";
        element.textContent = label;
        element.title = title;
        element.addEventListener("mousedown", function (event) {
            event.preventDefault();
        });
        element.addEventListener("click", action);

        return element;
    };

    var attach = function (quill) {
        var container = quill.container;
        var selected = null;
        var drag = null;

        var panel = document.createElement("div");
        var handle = document.createElement("div");

        panel.className = "ql-image-panel btn-group";
        panel.hidden = true;
        handle.className = "ql-image-handle";
        handle.hidden = true;
        container.appendChild(panel);
        container.appendChild(handle);

        var blotOf = function () {
            return selected && Quill.find(selected);
        };

        var apply = function (formats) {
            var blot = blotOf();

            if (!blot) {
                return;
            }

            quill.formatText(blot.offset(quill.scroll), 1, formats, "user");
            place();
        };

        var place = function () {
            if (!selected || !container.contains(selected)) {
                deselect();

                return;
            }

            var box = container.getBoundingClientRect();
            var image = selected.getBoundingClientRect();

            panel.hidden = false;
            panel.style.top = Math.max(image.top - box.top - panel.offsetHeight - 4, 0) + "px";
            panel.style.left = Math.max(image.left - box.left, 0) + "px";
            handle.hidden = false;
            handle.style.top = image.bottom - box.top - handle.offsetHeight / 2 + "px";
            handle.style.left = image.right - box.left - handle.offsetWidth / 2 + "px";
            selected.classList.add("ql-image-selected");
        };

        var deselect = function () {
            if (selected) {
                selected.classList.remove("ql-image-selected");
            }

            selected = null;
            panel.hidden = true;
            handle.hidden = true;
        };

        var widthOf = function (percent) {
            return Math.round(quill.root.clientWidth * percent / 100);
        };

        sizes.forEach(function (percent) {
            panel.appendChild(button(percent + "%", "Resize to " + percent + "% of the editor width", function () {
                apply({ width: String(widthOf(percent)), height: null });
            }));
        });

        panel.appendChild(button("Original", "Original size", function () {
            apply({ width: null, height: null });
        }));

        [["Left", "left", "Place on the left, text wraps around"],
        ["Center", "center", "Place in the center"],
        ["Right", "right", "Place on the right, text wraps around"],
        ["Inline", null, "Place inline with the text"]].forEach(function (entry) {
            panel.appendChild(button(entry[0], entry[2], function () {
                apply({ imageAlign: entry[1] });
            }));
        });

        quill.root.addEventListener("click", function (event) {
            if (event.target instanceof HTMLImageElement) {
                deselect();
                selected = event.target;
                place();
            } else {
                deselect();
            }
        });

        quill.root.addEventListener("scroll", place);
        quill.on("text-change", function () {
            if (selected) {
                window.requestAnimationFrame(place);
            }
        });
        window.addEventListener("resize", place);

        handle.addEventListener("pointerdown", function (event) {
            if (!selected) {
                return;
            }

            event.preventDefault();
            handle.setPointerCapture(event.pointerId);
            drag = { x: event.clientX, width: selected.getBoundingClientRect().width };
        });

        handle.addEventListener("pointermove", function (event) {
            if (!drag || !selected) {
                return;
            }

            var width = Math.min(Math.max(drag.width + event.clientX - drag.x, minimumWidth), quill.root.clientWidth);

            selected.setAttribute("width", String(Math.round(width)));
            selected.removeAttribute("height");
            place();
        });

        handle.addEventListener("pointerup", function (event) {
            if (!drag) {
                return;
            }

            handle.releasePointerCapture(event.pointerId);
            drag = null;
            apply({ width: selected.getAttribute("width"), height: null });
        });

        return { deselect: deselect };
    };

    return { register: register, attach: attach };
});
