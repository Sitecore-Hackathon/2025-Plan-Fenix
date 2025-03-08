const Spinner = ({ className = "w-10 h-10" }) => {
  return (
    <div className="mx-auto my-auto h-screen w-screen">
      <svg
        className={className}
        viewBox="0 0 50 50"
        xmlns="http://www.w3.org/2000/svg"
      >
        {/* Outer circle (track) */}
        <circle
          cx="25"
          cy="25"
          r="20"
          fill="none"
          stroke="#E2E8F0"
          strokeWidth="4"
        />

        {/* Spinner arc that rotates */}
        <circle
          cx="25"
          cy="25"
          r="20"
          fill="none"
          stroke="#4A5568"
          strokeWidth="4"
          strokeLinecap="round"
          strokeDasharray="94.2"
          strokeDashoffset="50">
          <animateTransform
            attributeName="transform"
            type="rotate"
            from="0 25 25"
            to="360 25 25"
            dur="1s"
            repeatCount="indefinite"
          />
        </circle>
      </svg>
    </div>
  );
};

export default Spinner;