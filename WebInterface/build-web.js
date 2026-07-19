// Ensure webpack 4 works on modern Node (OpenSSL 3).
const { spawnSync } = require("child_process");
const path = require("path");

const env = { ...process.env };
const legacy = "--openssl-legacy-provider";
const current = env.NODE_OPTIONS || "";
if (!current.includes("openssl-legacy-provider")) {
	env.NODE_OPTIONS = (current + " " + legacy).trim();
}

const webpackCli = path.join(__dirname, "node_modules", "webpack", "bin", "webpack.js");
const result = spawnSync(process.execPath, [webpackCli, "--config", "webpack.production.config.js"], {
	cwd: __dirname,
	env,
	stdio: "inherit",
});
process.exit(result.status == null ? 1 : result.status);
