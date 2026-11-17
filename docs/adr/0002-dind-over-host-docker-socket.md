# 2. Dedicated Docker-in-Docker daemon for sandboxes

Status: accepted · 2026-11-17

## Context
The worker must start one container per submission. Mounting `/var/run/docker.sock` gives whoever
controls the worker root on the host.

## Decision
Run a `docker:dind` `sandbox-daemon` on the internal `exec` network (plus `egress` for image pulls).
The worker talks to it over `tcp://sandbox-daemon:2375` and never sees the host socket. Every sandbox
runs with `NetworkMode=none`, read-only rootfs, `CapDrop ALL`, `no-new-privileges`, uid 10001, pids,
memory (= swap) and CPU limits, and a worker-enforced wall-clock timeout. gVisor (`runsc`) where available.

## Consequences
- DinD needs `privileged: true`; TLS is off because `exec` is internal with a single client.
- Sandbox images must be pulled into (or `docker load`ed into) the nested daemon.
- Local isolation is still weaker than separate servers; treat hardening as ongoing.
