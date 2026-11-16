define(["models/i18n"], function (i18n) {
    var errorMessages = {
        NotAllowedError: "auth.twoFactor.passkeyCancelled",
        AbortError: "auth.twoFactor.passkeyCancelled",
        InvalidStateError: "auth.twoFactor.passkeyAlreadyRegistered"
    };

    var toBase64Url = function (buffer) {
        var binary = Array.prototype.map.call(new Uint8Array(buffer), function (value) {
            return String.fromCharCode(value);
        }).join("");

        return window.btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    };

    var toBuffer = function (value) {
        var padded = value.replace(/-/g, "+").replace(/_/g, "/");
        var binary = window.atob(padded + "=".repeat((4 - padded.length % 4) % 4));

        return Uint8Array.from(binary, function (character) {
            return character.charCodeAt(0);
        }).buffer;
    };

    var compact = function (value) {
        if (Array.isArray(value)) {
            return value.map(compact);
        }

        if (value !== null && typeof value === "object") {
            return Object.keys(value).reduce(function (result, key) {
                if (value[key] !== null && value[key] !== undefined) {
                    result[key] = compact(value[key]);
                }

                return result;
            }, {});
        }

        return value;
    };

    var descriptor = function (item) {
        return Object.assign({}, item, { id: toBuffer(item.id) });
    };

    var creationOptions = function (options) {
        var clean = compact(options);

        return Object.assign({}, clean, {
            challenge: toBuffer(clean.challenge),
            user: Object.assign({}, clean.user, { id: toBuffer(clean.user.id) }),
            excludeCredentials: (clean.excludeCredentials || []).map(descriptor)
        });
    };

    var requestOptions = function (options) {
        var clean = compact(options);

        return Object.assign({}, clean, {
            challenge: toBuffer(clean.challenge),
            allowCredentials: (clean.allowCredentials || []).map(descriptor)
        });
    };

    var attestation = function (credential) {
        var response = credential.response;

        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            response: {
                attestationObject: toBase64Url(response.attestationObject),
                clientDataJSON: toBase64Url(response.clientDataJSON),
                transports: typeof response.getTransports === "function" ? response.getTransports() : undefined
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    };

    var assertion = function (credential) {
        var response = credential.response;

        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            response: {
                authenticatorData: toBase64Url(response.authenticatorData),
                signature: toBase64Url(response.signature),
                clientDataJSON: toBase64Url(response.clientDataJSON),
                userHandle: response.userHandle ? toBase64Url(response.userHandle) : null
            },
            clientExtensionResults: credential.getClientExtensionResults()
        };
    };

    var supported = function () {
        return !!window.PublicKeyCredential && !!navigator.credentials;
    };

    var rejected = function (key) {
        return { ok: false, message: i18n.t(key) };
    };

    var invoke = function (call, encode) {
        if (!supported()) {
            return Promise.resolve(rejected("auth.twoFactor.passkeyUnsupported"));
        }

        return Promise.resolve()
            .then(call)
            .then(function (credential) {
                return credential
                    ? { ok: true, credential: encode(credential) }
                    : rejected("auth.twoFactor.passkeyFailed");
            })
            .catch(function (error) {
                return rejected(errorMessages[error && error.name] || "auth.twoFactor.passkeyFailed");
            });
    };

    return {
        supported: supported,
        create: function (options) {
            return invoke(function () {
                return navigator.credentials.create({ publicKey: creationOptions(options) });
            }, attestation);
        },
        get: function (options) {
            return invoke(function () {
                return navigator.credentials.get({ publicKey: requestOptions(options) });
            }, assertion);
        }
    };
});
