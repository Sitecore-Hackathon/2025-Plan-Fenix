import { useCleanedData } from "../api/response";

const RichText = () => {
  const { data } = useCleanedData();

  return (
    <div
      className="text-justify text-lg"
      dangerouslySetInnerHTML={{ __html: data?.content || '' }}
    />
  );
};

export default RichText;