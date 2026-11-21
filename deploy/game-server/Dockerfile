FROM ubuntu:24.04
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates libglib2.0-0t64 libnss3 libstdc++6 libgcc-s1 zlib1g \
    && rm -rf /var/lib/apt/lists/* && useradd --create-home --uid 10001 unity
WORKDIR /app
ARG SERVER_BUILD_PATH=artifacts/account-multiplayer-ai-20261103/linux-server
COPY ${SERVER_BUILD_PATH}/ /app/
RUN chmod +x /app/PickleVerseServer && mkdir -p /home/unity/.config/unity3d \
    && chown -R unity:unity /app /home/unity/.config
USER unity
EXPOSE 3669/udp
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=3 CMD test -f /tmp/pickleverse-worker-alive && test $(($(date +%s) - $(stat -c %Y /tmp/pickleverse-worker-alive))) -lt 20
ENTRYPOINT ["/app/PickleVerseServer", "-batchmode", "-nographics", "-logFile", "-", "-pkvNetworkDiagnostics"]
