import { useCleanedData } from "../api/response";

const Title = () => {
  const { data } = useCleanedData();

  return (
    <h1 className="text-center text-5xl font-bold mb-20">
      {data?.title || 'Suggested Images'}
    </h1>
  );
};

export default Title;