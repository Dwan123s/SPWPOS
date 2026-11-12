import { createRoot } from "react-dom/client";
import { App } from "./App.jsx";
import "./styles/index.css";
import "./styles/workspace-theme.css";
createRoot(document.getElementById("app")).render(<App />);
