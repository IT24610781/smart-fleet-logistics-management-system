export default function PageHeading({ eyebrow, title, description }) {
  return <div className="fleet-page-heading">
    <span className="fleet-page-eyebrow">{eyebrow}</span>
    <h1 className="fleet-page-title">{title}</h1>
    {description && <p className="fleet-page-description">{description}</p>}
  </div>
}
