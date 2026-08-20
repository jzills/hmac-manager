/**
 * The parts of a Node `IncomingMessage` this adapter reads.
 *
 * Described structurally rather than imported from `node:http` so that neither this
 * package nor anything consuming it needs `@types/node`. An `IncomingMessage` satisfies
 * it as-is; so does Express's `Request`, which extends it. The alternative — importing
 * the real type — would put `node:http` in the published declarations and break the
 * typecheck of every browser-only consumer, to describe four properties.
 */
export type NodeRequestLike = {
    /** The request target, path and query. */
    url?: string;

    /** The HTTP method. */
    method?: string;

    /** The request headers, lowercased by Node, with repeated ones as arrays. */
    headers: Record<string, string | string[] | undefined>;

    /**
     * The underlying socket, inspected only for whether TLS terminated here.
     *
     * Typed loosely on purpose: naming the one property that matters would make this a
     * weak type, which a real `Socket` — having every property except that one — is not
     * assignable to.
     */
    socket?: unknown;
};

/**
 * How to turn a Node request into the Fetch `Request` that `verify` expects.
 */
export type FromNodeRequestOptions = {
    /**
     * The raw request body, exactly as it arrived on the wire.
     *
     * Required for any request that has one. Node does not buffer the body, and a body
     * parser that has already consumed it leaves only its parsed form behind — and
     * `JSON.stringify` of that is not byte-identical to what the caller signed. Key
     * order, whitespace and number formatting are all free to differ, so the content
     * hash differs, so the signature does not match, and the failure looks exactly like
     * a forged request.
     *
     * With Express, capture it during parsing:
     *
     * ```ts
     * app.use(express.json({
     *     verify: (req, _res, buf) => { (req as any).rawBody = buf; }
     * }));
     * ```
     */
    body?: Uint8Array | string;

    /**
     * The origin the caller addressed, as `https://host:port`.
     *
     * Overrides everything below. Use it where the request cannot say for itself — a
     * Unix socket, or a proxy that rewrites the Host header.
     */
    baseUrl?: string;

    /**
     * Whether to believe `x-forwarded-proto` and `x-forwarded-host`.
     *
     * Off by default, and that default is the security-relevant one: those headers are
     * ordinary request headers, so anything that can reach this process directly can set
     * them. They are only trustworthy where a proxy is guaranteed to overwrite them,
     * which the process itself cannot verify — hence an explicit opt-in.
     *
     * Turn it on when running behind an ingress or load balancer that terminates TLS,
     * because otherwise the scheme seen here is `http` while the caller signed `https`.
     */
    trustProxy?: boolean;
};

/**
 * Builds a Fetch `Request` from a Node `IncomingMessage`, so requests arriving through
 * `node:http`, Express, Koa or Fastify can be verified.
 *
 * Runtimes with a native `Request` — Hono, Next.js route handlers, Deno, Bun,
 * Cloudflare Workers — already have one and do not need this.
 *
 * Reconstructing the URL is the delicate part, and the reason this exists rather than
 * being left to each caller. The signing content covers the host, path and query, so a
 * URL rebuilt even slightly differently from the one the caller signed produces a
 * signature mismatch — reported identically to a forgery, and with nothing in the
 * request to suggest the verifier was the one that got it wrong.
 *
 * @param request - The incoming Node request.
 * @param options - Body and origin handling. See {@link FromNodeRequestOptions}.
 * @returns A `Request` carrying the same method, URL, headers and body.
 */
export const fromNodeRequest = (
    request: NodeRequestLike,
    options: FromNodeRequestOptions = {}
): Request => {
    const url = new URL(request.url ?? "/", getOrigin(request, options));
    const method = request.method ?? "GET";

    const headers = new Headers();
    for (const [name, value] of Object.entries(request.headers)) {
        if (value === undefined) {
            continue;
        }

        // A repeated header arrives as an array. Appended one by one rather than joined,
        // so the Headers instance does its own comma-folding and a value that itself
        // contains a comma is not silently turned into two.
        for (const single of Array.isArray(value) ? value : [value]) {
            headers.append(name, single);
        }
    }

    // GET and HEAD may not carry a body; passing one throws rather than being ignored,
    // so a caller who supplies a body for one is told about it here.
    const hasBody = options.body !== undefined && method !== "GET" && method !== "HEAD";

    return new Request(url, {
        method,
        headers,
        ...(hasBody ? { body: toBodyInit(options.body!) } : {})
    });
};

/**
 * Presents a body as something `Request` will accept.
 *
 * Bytes are copied into a fresh array rather than passed through. A `Buffer` — which is
 * what a Node body parser hands back — is backed by a pooled, possibly shared buffer,
 * and `BodyInit` admits only `ArrayBuffer`-backed views. The copy also detaches the body
 * from Node's pool, so a later write into that pool cannot alter what was hashed.
 */
const toBodyInit = (body: Uint8Array | string): BodyInit =>
    typeof body === "string" ? body : new Uint8Array(body);

/**
 * Works out the origin the caller addressed.
 */
const getOrigin = (request: NodeRequestLike, options: FromNodeRequestOptions): string => {
    if (options.baseUrl !== undefined) {
        return options.baseUrl;
    }

    /**
     * Reads a header, taking the first value where there are several.
     *
     * A chain of proxies appends to the `x-forwarded-*` headers, and the client-facing
     * proxy — the one that saw the origin the caller actually signed — is at the front.
     * The same applies whether the chain arrived as a comma-joined string or as repeated
     * headers, so both are unwrapped here.
     *
     * Matched case-insensitively. Node lowercases incoming header names, so this only
     * matters for something else shaped like an IncomingMessage, where getting it wrong
     * would surface as "no Host header" on a request that plainly has one.
     */
    const header = (name: string): string | undefined => {
        const entry = Object.entries(request.headers)
            .find(([key]) => key.toLowerCase() === name);

        const value = Array.isArray(entry?.[1]) ? entry?.[1][0] : entry?.[1];
        return value === undefined ? undefined : value.split(",")[0].trim();
    };

    // `encrypted` is present on a TLSSocket and absent otherwise, which is the only
    // first-hand evidence this process has about the scheme. Optional throughout: a real
    // IncomingMessage always has a socket, but this accepts anything shaped like one.
    const isEncrypted = (request.socket as { encrypted?: boolean } | undefined)?.encrypted === true;

    const protocol = (options.trustProxy === true ? header("x-forwarded-proto") : undefined) ??
        (isEncrypted ? "https" : "http");

    const host = (options.trustProxy === true ? header("x-forwarded-host") : undefined) ??
        header("host");

    if (host === undefined || host.length === 0) {
        throw new Error(
            "Cannot determine the request origin: no Host header. Pass baseUrl to fromNodeRequest.");
    }

    return `${protocol}://${host}`;
};

export default fromNodeRequest;
