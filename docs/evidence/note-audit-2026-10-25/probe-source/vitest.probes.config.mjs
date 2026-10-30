import base from "../../frontend/vite.config.js";
import {fileURLToPath} from "node:url";
const at=relative=>fileURLToPath(new URL(relative,import.meta.url));
export default {...base,root:at("../../"),resolve:{alias:[
 {find:/^react$/,replacement:at("../../frontend/node_modules/react/index.js")},
 {find:/^react\/(.*)$/,replacement:at("../../frontend/node_modules/react/")+"$1"},
 {find:"@testing-library/react",replacement:at("../../frontend/node_modules/@testing-library/react/dist/index.js")},
 {find:"vitest",replacement:at("../../frontend/node_modules/vitest/dist/index.js")}
]},test:{...base.test,env:{NOTE_AUDIT_OUTPUT:at("ui-probes.json")},setupFiles:[at("../../frontend/tests/setup.js")],include:["artifacts/note-audit-2026-10-25/ui.probes.test.jsx"],maxWorkers:1}};
