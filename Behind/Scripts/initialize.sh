#!/usr/bin/env bash

set -e

rm -rf ~/.config/nvim ~/.local/share/nvim ~/.local/state/nvim ~/.cache/nvim
rm -rf ~/.local/share/netcoredbg ~/.local/bin/netcoredbg
rm -rf ~/.config/nushell ~/.cache/carapace

curl https://mise.run | sh
export PATH=/root/.local/bin:/root/.local/share/mise/shims:/root/.local/share/mise/bin:/root/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
eval "$(~/.local/bin/mise activate bash)"

mise use --global aqua:nushell/nushell@latest \
    && mise use --global aqua:carapace-sh/carapace-bin@latest \
    && mise use --global neovim@latest \
    && mise use --global tree-sitter@latest \
    && mise use --global dotnet@10.0 \
    && mise use --global node@lts

dotnet tool install --global aspire.cli || true
dotnet tool install --global dotnet-ef || true

mkdir -p ~/.local/share/netcoredbg
ARCH=$(uname -m)
if [ "$ARCH" = "x86_64" ]; then PKG_ARCH="amd64"; else PKG_ARCH="arm64"; fi
curl -sSL "https://github.com/Samsung/netcoredbg/releases/download/3.1.3-1062/netcoredbg-linux-${PKG_ARCH}.tar.gz" | tar -xzf - -C ~/.local/share/netcoredbg
ln -sf ~/.local/share/netcoredbg/netcoredbg ~/.local/bin/netcoredbg

echo "eval \"\$($HOME/.local/bin/mise activate bash)\"" >> ~/.bashrc
echo 'if [ -t 1 ] && [ -t 0 ]; then exec nu; fi' >> ~/.bashrc

mkdir -p ~/.config/nushell ~/.cache/carapace

echo "\$env.PATH = (\$env.PATH | split row (char esep) | prepend \"$HOME/.local/bin\" | prepend \"$HOME/.dotnet/tools\" | prepend \"$HOME/.local/share/mise/shims\")" > ~/.config/nushell/env.nu
echo '$env.config = { show_banner: false }' >> ~/.config/nushell/env.nu
echo '$env.EDITOR = "nvim"' >> ~/.config/nushell/env.nu

carapace _carapace nushell > ~/.cache/carapace/init.nu

cat <<'EOF' > ~/.config/nushell/config.nu
source ~/.cache/carapace/init.nu
alias n = nvim
def "nu-complete dotnet" [context: string, offset: int] {
    let raw_prefix = ($context | str substring 0..<$offset)
    ^dotnet complete $raw_prefix | lines
}
extern "dotnet" [ ...args: string@"nu-complete dotnet" ]
extern "aspire" [ ...args: string@"nu-complete dotnet" ]
def --wrapped d [...args: string@"nu-complete dotnet"] { dotnet ...$args }
def --wrapped a [...args: string@"nu-complete dotnet"] { aspire ...$args }
def --wrapped e [...args] { dotnet ef ...$args }
EOF

git clone https://github.com/NvChad/starter ~/.config/nvim

cat <<'EOF' > ~/.config/nvim/lua/plugins/init.lua
return {
  { "williamboman/mason.nvim", opts = { ensure_installed = { "roslyn", "html-lsp", "css-lsp", "json-lsp" } } },
  { "seblj/roslyn.nvim", ft = "cs", opts = {} },
  {
    "mfussenegger/nvim-dap",
    config = function()
      local dap = require("dap")
      dap.adapters.coreclr = { type = 'executable', command = 'netcoredbg', args = {'--interpreter=vscode'} }
      dap.configurations.cs = {
        { type = "coreclr", name = "Launch", request = "launch", 
          program = function() return vim.fn.input('Path to dll: ', vim.fn.getcwd() .. '/bin/Debug/net10.0/', 'file') end,
        },
      }
    end
  },
  { "rcarriga/nvim-dap-ui", dependencies = { "mfussenegger/nvim-dap", "nvim-neotest/nvim-nio" }, opts = {} },
  { "nvim-treesitter/nvim-treesitter", opts = { ensure_installed = { "c_sharp", "html", "lua", "nu" }, highlight = { enable = true } } }
}
EOF

cat <<'EOF' > ~/.config/nvim/lua/mappings.lua
local map = vim.keymap.set
vim.opt.timeoutlen = 500
map("n", "<F5>", function() require("dap").continue() end)
map("n", "<F9>", function() require("dap").toggle_breakpoint() end)
map("n", "<F10>", function() require("dap").step_over() end)
map("n", "<F11>", function() require("dap").step_into() end)
map("n", "<F12>", function() require("dapui").toggle() end)
map("n", "<leader>e", "<cmd>NvimTreeToggle<cr>")
EOF

cat <<'EOF' > ~/.config/nvim/lua/chadrc.lua
local M = {}
M.plugins = "plugins.init"
return M
EOF

nvim --headless "+Lazy! sync" +qa

mkdir -p /etc/containers && \
    cat <<'EOF' > /etc/containers/containers.conf
[containers]
log_driver = "json-file"

[engine]
events_logger = "file"
EOF


