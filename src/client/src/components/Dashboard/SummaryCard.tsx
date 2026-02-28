interface SummaryCardProps {
  title: string;
  value: number;
  icon: React.ReactNode;
  variant?: "primary" | "success" | "warning";
}

export function SummaryCard({ title, value, icon, variant = "primary" }: SummaryCardProps) {
  const variantStyles = {
    primary: {
      text: "text-white",
      bg: "bg-slate-700/50",
      border: "border-slate-600/30",
      icon: "text-primary-400",
      container: "border-slate-700 bg-slate-800",
    },
    success: {
      text: "text-success",
      bg: "bg-slate-700/50",
      border: "border-slate-600/30",
      icon: "text-success",
      container: "border-slate-700 bg-slate-800",
    },
    warning: {
      text: "text-warning",
      bg: "bg-warning/10",
      border: "border-warning/20",
      icon: "text-warning",
      container: value > 0 ? "border-warning/30 bg-warning/5" : "border-slate-700 bg-slate-800",
    },
  };

  const styles = variantStyles[variant];

  return (
    <div className={`p-6 rounded-2xl border shadow-sm transition-colors ${styles.container}`}>
      <div className="flex justify-between items-start">
        <div>
          <p className={`text-xs font-bold uppercase tracking-wider ${
            variant === "warning" && value > 0 ? "text-warning" : "text-slate-400"
          }`}>
            {title}
          </p>
          <h3 className={`text-3xl font-bold mt-2 ${styles.text}`}>
            {value}
          </h3>
        </div>
        <div className={`p-3 rounded-xl border ${styles.bg} ${styles.border}`}>
          <div className={`w-6 h-6 ${styles.icon}`}>
            {icon}
          </div>
        </div>
      </div>
    </div>
  );
}
