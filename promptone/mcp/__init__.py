"""MCP (Model Context Protocol) stdio server for PromptOne (optional extra).

Install the extra to use it:

    pip install "promptone[mcp]"

Then run the server:

    promptone-mcp-server            # console script
    python -m promptone.mcp.server  # equivalent module form

The submodules ``device``, ``remote`` and ``tools`` are importable without the
``mcp`` dependency; only ``server`` requires the extra.
"""

__all__ = ["device", "remote", "server", "tools"]
