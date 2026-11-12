// Browser clipboard for PurrLobby WebGL builds.
// GUIUtility.systemCopyBuffer only writes Unity's internal buffer in WebGL, so nothing
// reaches the OS clipboard. Browsers only allow clipboard writes shortly after a user
// gesture. Unity runs UI click handlers on the next frame, which Chromium accepts
// (transient activation lasts a few seconds) but stricter browsers may not, so if
// every attempt fails we copy again on the next click or key press on the page.
var PurrLobbyClipboard = {
    PurrLobby_CopyToClipboard: function (textPtr) {
        var text = UTF8ToString(textPtr);

        function execCommandCopy() {
            var active = document.activeElement;
            var area = document.createElement("textarea");
            area.value = text;
            area.setAttribute("readonly", "");
            area.style.position = "fixed";
            area.style.top = "0";
            area.style.left = "0";
            area.style.opacity = "0";
            area.style.pointerEvents = "none";
            document.body.appendChild(area);
            var ok = false;
            try {
                area.focus();
                area.select();
                area.setSelectionRange(0, text.length);
                ok = document.execCommand("copy");
            } catch (e) {
                ok = false;
            }
            document.body.removeChild(area);
            if (active && typeof active.focus === "function") {
                try { active.focus(); } catch (e) { }
            }
            return ok;
        }

        function copyOnNextGesture() {
            var events = ["pointerup", "keyup", "touchend"];
            function handler() {
                for (var i = 0; i < events.length; i++)
                    document.removeEventListener(events[i], handler, true);
                if (navigator.clipboard && window.isSecureContext) {
                    navigator.clipboard.writeText(text).catch(function () { execCommandCopy(); });
                } else {
                    execCommandCopy();
                }
            }
            for (var i = 0; i < events.length; i++)
                document.addEventListener(events[i], handler, true);
        }

        function fallback() {
            if (!execCommandCopy())
                copyOnNextGesture();
        }

        if (navigator.clipboard && window.isSecureContext) {
            try {
                navigator.clipboard.writeText(text).catch(fallback);
            } catch (e) {
                fallback();
            }
        } else {
            fallback();
        }
    }
};

mergeInto(LibraryManager.library, PurrLobbyClipboard);
