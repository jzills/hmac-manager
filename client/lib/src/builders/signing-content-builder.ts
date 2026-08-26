import SigningContentContext from "./signing-content-context";
import MissingHeaderError from "../exceptions/missing-header-error";

/**
 * Builder for constructing the signing content used in HMAC authentication.
 */
export default class SigningContentBuilder {
    protected context: SigningContentContext = {
        request: null,
        contentHash: null,
        dateRequested: new Date(),
        nonce: "",
        publicKey: "",
        signedHeaders: []
    };

    withRequest(request: Request) {
        this.context.request = request;
        return this;
    }

    /** 
     * Adds the public key to the signing content.
     * @param publicKey The public key to include.
     * @returns The instance of the builder for method chaining.
     */
    withPublicKey = (publicKey: string) => {
        this.context.publicKey = publicKey;
        return this;
    }

    /** 
     * Adds the date when the request was made to the signing content.
     * @param dateRequested The date of the request.
     * @returns The instance of the builder for method chaining.
     */
    withDateRequested = (dateRequested: Date) => {
        this.context.dateRequested = dateRequested;
        return this;
    }

    /** 
     * Adds the content hash to the signing content.
     * @param contentHash The content hash to include.
     * @returns The instance of the builder for method chaining.
     */
    withContentHash = (contentHash: string | null) => {
        this.context.contentHash = contentHash;
        return this;
    }

    /** 
     * Adds a nonce (unique identifier) to the signing content.
     * @param nonce The nonce to include.
     * @returns The instance of the builder for method chaining.
     */
    withNonce = (nonce: string) => {
        this.context.nonce = nonce;
        return this;
    }

    /**
     * Adds signed headers to the signing content.
     * Throws an error if any required headers are missing.
     * @param signedHeaders The headers that are signed.
     * @param headers The Headers object to extract header values from.
     * @returns The instance of the builder for method chaining.
     */
    withSignedHeaders = (signedHeaders: string[] = []) => {
        if (signedHeaders.length) {
            const headerValues = signedHeaders.map(key => this.context.request?.headers.get(key));
            const isMissingHeaderValues = headerValues.some(value => value === null);
            if (isMissingHeaderValues) {
                // A typed error rather than a bare one. This same builder runs on both
                // sides now, and the verifier has to tell "the caller omitted a header
                // the scheme covers" — a rejected request — apart from a fault of its
                // own; it cannot do that by matching on a message.
                throw new MissingHeaderError(
                    "There are missing headers that are required for signing.");
            } else {
                this.context.signedHeaders = headerValues as string[];
            }
        }

        return this;
    }

    createBuilder = () => new SigningContentBuilder();

    /** 
     * Builds the signing content by joining the individual components.
     * @returns The constructed signing content as a string.
     */
    build = async () => {
        const { method, url } = this.context.request as Request;
        const { host, search, pathname } = new URL(url);

        const stringBuilder: string[] = [];
        stringBuilder.push(method);
        stringBuilder.push(`${pathname}${search}`);
        stringBuilder.push(host);
        stringBuilder.push(this.context.dateRequested.getTime().toString());
        stringBuilder.push(this.context.publicKey);
        
        if (this.context.contentHash) {
            stringBuilder.push(this.context.contentHash);
        }

        stringBuilder.push(...this.context.signedHeaders);
        stringBuilder.push(this.context.nonce);
        return stringBuilder.join(":");
    }
}