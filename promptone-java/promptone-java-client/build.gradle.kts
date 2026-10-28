// The HTTP client for a self-hosted `promptone-serve`: three routes, no inference. Peer of the
// `promptone-client` npm package. `java.net.http` is in the JDK, so this has no runtime dependency
// either; it shares the runtime's answer types.
dependencies {
    api(project(":promptone-java"))
}
