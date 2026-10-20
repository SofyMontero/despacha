/** Isotipo Monteblanco en SVG (nitido en UI pequeña) */
export default function MonteblancoMark({ size = 28, className = '' }) {
  return (
    <svg
      className={className}
      width={size}
      height={size * 0.62}
      viewBox="0 0 120 74"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <path
        d="M8 62 L38 12 L60 46"
        stroke="#2B2F33"
        strokeWidth="14"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M52 46 L74 12 L112 62"
        stroke="#5D8F8A"
        strokeWidth="14"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
