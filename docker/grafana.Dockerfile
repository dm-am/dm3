# Grafana with the VictoriaLogs datasource, which the image does not carry. The plugin is
# fetched here, once per build, and checked against the sha256 GitHub records for the release
# asset (the release's own checksum file is SHA-1); an install at container start would reach
# the internet on every start. A bump changes both the line here and the image name in
# docker-compose.yml, because a server compares that name to decide whether to rebuild.
FROM grafana/grafana:13.3.1

ARG PLUGIN_VERSION=v0.34.0
ARG PLUGIN_SHA256=72c7dbe4bc046f278670f6ab4c4b1518201037f35f534eff462f76fa6ef771be

# Outside /var/lib/grafana: a volume there would keep an old plugin across image updates.
ENV GF_PATHS_PLUGINS=/usr/share/grafana/dm-plugins

USER root
RUN wget -q -O /tmp/plugin.tar.gz \
        "https://github.com/VictoriaMetrics/victorialogs-datasource/releases/download/${PLUGIN_VERSION}/victoriametrics-logs-datasource-${PLUGIN_VERSION}.tar.gz" \
    && echo "${PLUGIN_SHA256}  /tmp/plugin.tar.gz" | sha256sum -c - \
    && mkdir -p "${GF_PATHS_PLUGINS}" \
    && tar -xzf /tmp/plugin.tar.gz -C "${GF_PATHS_PLUGINS}" \
    && rm /tmp/plugin.tar.gz
# The image's own user, as the base sets it.
USER 472
