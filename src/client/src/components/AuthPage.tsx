import { useState } from "react";
import { Login } from "./Login";
import { RegisterAccount } from "./RegisterAccount";

interface AuthPageProps {
  onSetupRequired?: () => void;
}

export function AuthPage({ onSetupRequired }: AuthPageProps) {
  const [mode, setMode] = useState<"login" | "register">("login");

  if (mode === "register") {
    return <RegisterAccount onBackToLogin={() => setMode("login")} />;
  }

  return (
    <Login
      onSetupRequired={onSetupRequired}
      onCreateAccount={() => setMode("register")}
    />
  );
}
