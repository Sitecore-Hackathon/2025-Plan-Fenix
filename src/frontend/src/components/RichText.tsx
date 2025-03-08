interface RichTextProps {
  text: string;
}

const RichText: React.FC<RichTextProps> = ({ text }) => {
  return (
    <p>{text}</p>
  );
};

export default RichText;